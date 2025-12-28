using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MewVivor.Common;
using MewVivor.Data;
using MewVivor.Enum;
using MewVivor.Factory;
using MewVivor.InGame.Controller;
using MewVivor.InGame.Entity;
using MewVivor.Managers;
using MewVivor.Model;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MewVivor.InGame.Stage
{
    public abstract class StageBase : IDisposable
    {
        public MapController CurrentMapController => _currentMapController;
        public DateTime GameStartTime { get; protected set; }

        protected MapController _currentMapController;
        protected readonly ObjectManager _object = Manager.I.Object;
        protected readonly ResourcesManager _resource = Manager.I.Resource;
        protected CancellationTokenSource _waveCts;
        protected CancellationTokenSource _dropItemCts;
        protected StageModel _stageModel;

        public virtual void StartGame() {}

        public virtual void StopGame()
        {
            StopWave();
            Utils.SafeCancelCancellationTokenSource(ref _dropItemCts);
        }

        public virtual void StopWave()
        {
            Utils.SafeCancelCancellationTokenSource(ref _waveCts);
        }

        protected virtual async UniTask StartWave()
        {
            _waveCts = new CancellationTokenSource();
        }

        public virtual void OnDeadMonster(MonsterController monster)
        {
            SpawnDropItemByMonsterType(monster);
            ReceiveRewardItemByMonsterKill(monster);
        }

        public virtual void OnDeadMonster(MonsterDeadData monsterDeadData)
        {
            SpawnDropItemByMonsterType(monsterDeadData);
            ReceiveRewardItemByMonsterKill(monsterDeadData);
        }
        
        public abstract void SpawnDropItemByMonsterType(MonsterController monster);
        protected abstract void ReceiveRewardItemByMonsterKill(MonsterController monster);
        
        public abstract void SpawnDropItemByMonsterType(MonsterDeadData monsterDeadData);
        protected abstract void ReceiveRewardItemByMonsterKill(MonsterDeadData monsterDeadData);

        public void SpawnDropItem(DropItemData dropItemData, Vector3 spawnPosition, bool useRandomSpawnPosition = true)
        {
            float x = Random.Range(-3, 3);
            float y = Random.Range(-3, 3);
            string prefabName = dropItemData.DropItemType.ToString();
            GameObject prefab = _resource.Instantiate(prefabName);
            var dropItem = prefab.GetComponent<DropItemController>();
            dropItem.Spawn(spawnPosition +
                           (useRandomSpawnPosition ? new Vector3(x, y) : Vector3.zero), dropItemData);
            CurrentMapController.AddItemInGrid(spawnPosition, dropItem);
            _object.DroppedItemControllerList.Add(dropItem);
        }

        public virtual void OnBossKill()
        {
            Utils.SafeCancelCancellationTokenSource(ref _waveCts);
        }
        
        public virtual void ForceSpawnMonster(MonsterType monsterType) { }

        protected async UniTaskVoid SpawnDropItemBoxAsync()
        {
            _dropItemCts = new CancellationTokenSource();
            DataManager dataManager = Manager.I.Data;
            GlobalConfigData ingameItemBoxCycleConfigData =
                dataManager.GlobalConfigDataDict[GlobalConfigName.IngameItemBoxCycle];
            float interval = ingameItemBoxCycleConfigData.Value;
            interval *= Utils.GetPlayerStat(CreatureStatType.ItemBoxSpawnCoolTime);
            GlobalConfigData ingameItemBoxSpawnRangeConfigData =
                dataManager.GlobalConfigDataDict[GlobalConfigName.IngameItemBoxSpawnRange];
            float range = ingameItemBoxSpawnRangeConfigData.Value;
            PlayerController player = Manager.I.Object.Player;
            while (_dropItemCts != null && !_dropItemCts.IsCancellationRequested)
            {
                try
                {
                    await UniTask.WaitForSeconds(interval, cancellationToken: _dropItemCts.Token);
                }
                catch (Exception e) when(e is not OperationCanceledException)
                {
                    Debug.LogError($"error {e.Message}");
                    return;
                }

                float angle = Random.Range(0, 360);
                float x = Mathf.Cos(angle) * range;
                float y = Mathf.Sin(angle) * range;
                Vector3 spawnPosition = player.Position + new Vector3(x, y);
                DropItemData dropItemData = dataManager.DropItemDict[Const.ITEM_BOX_DATA_ID];
                SpawnDropItem(dropItemData, spawnPosition);
            }
        }
        
        protected virtual async UniTask WaveTimerAsync(int remainTime)
        {
            int timer = remainTime;
            int elapsed = 1;
            while (timer > 0)
            {
                _stageModel.WaveTimer.Value = timer;
                _stageModel.ElapsedGameTime.Value += elapsed;
                while (Manager.I.Game.GameState == GameState.DeadPlayer)
                {
                    try
                    {
                        await UniTask.Yield(cancellationToken: _waveCts.Token);
                        continue;
                    }
                    catch (Exception e) when (e is OperationCanceledException)
                    {
                        Debug.Log("Cancel");
                        return;
                    }
                    catch (Exception e) when (!(e is OperationCanceledException))
                    {
                        Debug.LogError("error wave time " + e);
                        return;
                    }
                }
                
                timer--;
                
                try
                {
                    await UniTask.WaitForSeconds(elapsed, cancellationToken: _waveCts.Token);
                }
                catch (Exception e) when(e is OperationCanceledException)
                {
                    Debug.Log("Cancel");
                    break;
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    Debug.LogError("error wave time " + e);
                    break;
                }
            }
        }

        protected int GetKillCount(StageModel model, MonsterType monsterType)
        {
            return monsterType switch
            {
                MonsterType.Normal => model.NormalMonsterKillCount.Value,
                MonsterType.Elite => model.EliteMonsterKillCount.Value,
                MonsterType.Boss => model.BossMonsterKillCount.Value,
                _ => 0
            };
        }

        public virtual void RaiseSpawnMonster(int spawnCount, List<int> monsterIdList, MonsterType monsterType)
        {
            StageModel stageModel = ModelFactory.CreateOrGetModel<StageModel>();
            int waveIndex = stageModel.CurrentWaveStep.Value;
            Vector3 spawnPosition = Vector3.zero;
            int index = 0;
            switch (monsterType)
            {
                case MonsterType.Normal:
                case MonsterType.SuicideBomber:
                    GlobalConfigData monsterLimitCount =
                        Manager.I.Data.GlobalConfigDataDict[GlobalConfigName.MonsterCountLimit];
                    int remainSpawnableCount = (int)monsterLimitCount.Value - Manager.I.Object.ActivateMonsterCount;
                    spawnCount = spawnCount > Const.MaxOnceSpawnNormalMonsterCount
                        ? Const.MaxOnceSpawnNormalMonsterCount
                        : spawnCount;
                    spawnCount = spawnCount > remainSpawnableCount ? remainSpawnableCount : spawnCount;
                    float angle = (float)360 / spawnCount;
                    for (int i = 0; i < spawnCount; i++)
                    {
                        float targetAngle = angle * i + Random.Range(-5, 5); //기계처럼 나오는 경우가 있는 것 같아서 살짝 랜덤 추가;
                        float radius = Random.Range(30, 45);
                        spawnPosition = Utils.GetCirclePosition(targetAngle, radius);
                        Manager.I.Object.SpawnMonster(monsterIdList[index], spawnPosition, monsterType, waveIndex);
                        index++;
                        index %= monsterIdList.Count;
                    }
                    break;
                case MonsterType.Elite:
                case MonsterType.Boss:
                    for (int i = 0; i < spawnCount; i++)
                    {
                        spawnPosition = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0));
                        spawnPosition.z = 0;
                        Manager.I.Object.SpawnMonster(monsterIdList[index], spawnPosition, monsterType, waveIndex);
                        index++;
                        index %= monsterIdList.Count;
                    }
                    break;
            }
        }

        protected virtual void GetRewardByBossKill(float prob, int rewardId, int minRange, int maxRange)
        {
            if (!(Random.value < prob))
            {
                return;
            }

            var playerModel = ModelFactory.CreateOrGetModel<PlayerModel>();
            int amount = Random.Range(minRange, maxRange);
            if (!playerModel.AcquiredRewardItemDict.ContainsKey(rewardId))
            {
                playerModel.AcquiredRewardItemDict[rewardId] = 0;
            }

            playerModel.AcquiredRewardItemDict[rewardId] += amount;
        }

        public void Dispose()
        {
            StopGame();
        }
    }
}