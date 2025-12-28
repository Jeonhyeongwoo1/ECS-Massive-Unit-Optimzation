using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MewVivor.Common;
using MewVivor.Data;
using MewVivor.Enum;
using MewVivor.Factory;
using MewVivor.InGame.Controller;
using MewVivor.InGame.Entity;
using MewVivor.Model;
using MewVivor.Presenter;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MewVivor.InGame.Stage
{
    public class NormalStage : StageBase, IDisposable
    {
        public int StageLevel => _stageData.StageLevel;
        public int StageIndex => _stageData.StageIndex;
        public WaveData CurrentWaveData => _waveData;
        
        private StageData _stageData;
        private WaveData _waveData;
        private CancellationTokenSource _stageCts;

        public NormalStage(StageData stageData, int waveIndex)
        {
            _stageModel = ModelFactory.CreateOrGetModel<StageModel>();
            _stageData = stageData;
            _waveData = _stageData.WaveList[waveIndex];
            _stageModel.CurrentWaveStep.Value = _waveData.WaveIndex;
            _stageModel.StageLevel.Value = stageData.StageLevel;

            Debug.Log($"stage level {_stageData.StageLevel} / wave {waveIndex} / wave index {_waveData.WaveIndex}");
            //맵, 몬스터 스폰, Wave
            GameObject map = Manager.I.Resource.Instantiate(_stageData.MapName, false);
            _currentMapController = map.GetComponent<MapController>();
            Manager.I.Object.CreatePlayer();
        }

        public override void StartGame()
        {
            base.StartGame();
            _stageCts = new CancellationTokenSource();
            StartStageAsync(0).Forget();
        }

        public override void StopGame()
        {
            base.StopGame();
            Utils.SafeCancelCancellationTokenSource(ref _stageCts);
        }

        private async UniTask StartStageAsync(int waveIndex)
        {
            GameStartTime = DateTime.UtcNow;
            int waveCount = _stageData.WaveList.Count;
            SpawnDropItemBoxAsync().Forget();
            for (int i = waveIndex; i < waveCount; i++)
            {
                try
                {
                    await StartWave().AttachExternalCancellation(_stageCts.Token);
                }
                catch (Exception e) when(e is not OperationCanceledException)
                {
                    Debug.LogError($"error {e.Message}");
                    return;
                }

                //Reward
                ReceiveRewardItemByWaveClear(_stageData.WaveList[i]);
                StopWave();

                int nextIndex = i + 1;
                if (nextIndex == waveCount)
                {
                    break;
                }

                _waveData = _stageData.WaveList[nextIndex];
                _stageModel.CurrentWaveStep.Value = _waveData.WaveIndex;
                Manager.I.GameContinueData.waveIndex = _waveData.WaveIndex;
                Manager.I.Event.Raise(GameEventType.EndWave);

                Debug.Log($"new wave {_waveData.WaveIndex} / stage level {_stageData.StageLevel}");
            }
            
            Manager.I.Event.Raise(GameEventType.CompletedStage);
        }

        protected override void ReceiveRewardItemByMonsterKill(MonsterController monster)
        {
            MonsterType monsterType = monster.MonsterType;
            Vector3 monsterPosition = monster.Position;
            if (monsterType == MonsterType.Boss)
            {
                int rewardId = _waveData.BossClearRewardId;
                float prob = _waveData.BossClearRewardProb;
                if (_waveData.BossClearRewardId == 0 || prob == 0)
                {
                    return;
                }
                
                int minRange = _waveData.BossClearRewardMinAmount;
                int maxRange = _waveData.BossClearRewardMaxAmount;
                GetRewardByBossKill(prob, rewardId, minRange, maxRange);
            }
            
            if (Random.value < _stageData.MonsterKillDropScrollprob)
            {
                PlayerModel model = ModelFactory.CreateOrGetModel<PlayerModel>();
                if(!model.AcquiredRewardItemDict.ContainsKey(Const.ID_RANDOM_SCROLL))
                {
                    model.AcquiredRewardItemDict[Const.ID_RANDOM_SCROLL] = 0;
                }

                if (model.AcquiredRewardItemDict[Const.ID_RANDOM_SCROLL] < _stageData.MaxDropScrollCount)
                {
                    model.AcquiredRewardItemDict[Const.ID_RANDOM_SCROLL]++;
                }
            }

            if (Random.value < _stageData.MosterKillDropLevelUpItemProb)
            {
                int id = Const.ID_SKILLUP;
                if (!Manager.I.Data.DropItemDict.TryGetValue(id, out DropItemData dropItemData))
                {
                    Debug.LogWarning($"failed spawn drop item {id}");
                    return;
                }

                SpawnDropItem(dropItemData, monsterPosition);
            }
        }

        protected override void ReceiveRewardItemByMonsterKill(MonsterDeadData monsterDeadData)
        {
            MonsterType monsterType = monsterDeadData.MonsterType;
            Vector3 monsterPosition = monsterDeadData.Position;
            if (monsterType == MonsterType.Boss)
            {
                int rewardId = _waveData.BossClearRewardId;
                float prob = _waveData.BossClearRewardProb;
                if (_waveData.BossClearRewardId == 0 || prob == 0)
                {
                    return;
                }
                
                int minRange = _waveData.BossClearRewardMinAmount;
                int maxRange = _waveData.BossClearRewardMaxAmount;
                GetRewardByBossKill(prob, rewardId, minRange, maxRange);
            }
            
            if (Random.value < _stageData.MonsterKillDropScrollprob)
            {
                PlayerModel model = ModelFactory.CreateOrGetModel<PlayerModel>();
                if(!model.AcquiredRewardItemDict.ContainsKey(Const.ID_RANDOM_SCROLL))
                {
                    model.AcquiredRewardItemDict[Const.ID_RANDOM_SCROLL] = 0;
                }

                if (model.AcquiredRewardItemDict[Const.ID_RANDOM_SCROLL] < _stageData.MaxDropScrollCount)
                {
                    model.AcquiredRewardItemDict[Const.ID_RANDOM_SCROLL]++;
                }
            }

            if (Random.value < _stageData.MosterKillDropLevelUpItemProb)
            {
                int id = Const.ID_SKILLUP;
                if (!Manager.I.Data.DropItemDict.TryGetValue(id, out DropItemData dropItemData))
                {
                    Debug.LogWarning($"failed spawn drop item {id}");
                    return;
                }

                SpawnDropItem(dropItemData, monsterPosition);
            }
        }

        private void ReceiveRewardItemByWaveClear(WaveData waveData)
        {
            List<int> waveClearRewardItemIdList = waveData.WaveClearRewardItemId;
            List<int> waveClearRewardItemAmountList = waveData.WaveClearRewardItemAmount;
            if (waveClearRewardItemIdList == null || waveClearRewardItemAmountList == null)
            {
                return;
            }

            PlayerModel model = ModelFactory.CreateOrGetModel<PlayerModel>();
            int count = waveClearRewardItemIdList.Count;
            for (int i = 0; i < count; i++)
            {
                int id =waveClearRewardItemIdList[i];
                int amount = waveClearRewardItemAmountList[i];
                if (!model.AcquiredRewardItemDict.ContainsKey(id))
                {
                    model.AcquiredRewardItemDict[id] = 0;
                }

                model.AcquiredRewardItemDict[id] += amount;
            }
        }
        
        protected override async UniTask StartWave()
        {
            await base.StartWave();

            SpawnMonsterAsync(_waveData).Forget();

            if (_waveData.WaveType == WaveType.Boss)
            {
                // if (Manager.I.Object.IsAliveBossMonster())
                // {
                //     //보스 몬스터가 살아있다면 게임 종료
                //     Manager.I.Event.Raise(GameEventType.GameOver);
                //     StopGame();
                // }

                while (_waveCts != null && !_waveCts.IsCancellationRequested)
                {
                    try
                    {
                        await UniTask.Yield(cancellationToken: _waveCts.Token);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Debug.LogError($"Error {e.Message}");
                        break;
                    }
                    catch (Exception e) when(e is OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            else
            {
                await WaveTimerAsync((int) _waveData.RemainsTime);
            }
        }

        public override void SpawnDropItemByMonsterType(MonsterDeadData monsterDeadData)
        {
            MonsterType monsterType = monsterDeadData.MonsterType;
            Vector3 monsterPosition = monsterDeadData.Position;
            switch (monsterType)
            {
                case MonsterType.Normal:
                    float value = Random.value;
                    bool isPossibleSpawnDropItem = value > _waveData.nonDropRate;
                    if (!isPossibleSpawnDropItem)
                    {
                        return;
                    }

                    GemType gemType = GemType.None;
                    float purpleGemDropRate = _waveData.PurpleGemDropRate;
                    float greenGemRatio = _waveData.GreenGemDropRate;
                    float blueGemRatio = _waveData.BlueGemDropRate;
                    float redGemDropRate = _waveData.RedGemDropRate;
                    bool isSuccess = Utils.TrySpawnGem(ref gemType, purpleGemDropRate, greenGemRatio, blueGemRatio,
                        redGemDropRate);
                    
                    if (isSuccess)
                    {
                        GemController gem = Manager.I.Object.MakeGem(gemType, monsterPosition);
                        if (gem != null)
                        {
                            CurrentMapController.AddItemInGrid(gem.transform.position, gem);
                        }
                    }
                    break;
                case MonsterType.Boss:
                case MonsterType.Elite:
                    int targetWaveIndex = monsterDeadData.SpawnedWaveIndex;
                    WaveData waveData = _stageData.WaveList.Find(v => v.WaveIndex == targetWaveIndex);
                    
                    if (waveData.EliteAndBossClearDropItemId == null ||
                        waveData.EliteAndBossClearDropItemAmount == null)
                    {
                        return;
                    }
                    
                    int idCount = waveData.EliteAndBossClearDropItemId.Count;
                    List<int> dropItemIdList = waveData.EliteAndBossClearDropItemId;
                    List<int> dropItemAmountList = waveData.EliteAndBossClearDropItemAmount;
                    for (int i = 0; i < idCount; i++)
                    {
                        int id = dropItemIdList[i];
                        if (!Manager.I.Data.DropItemDict.TryGetValue(id, out DropItemData dropItemData))
                        {
                            Debug.LogWarning($"failed spawn drop item {id}");
                            continue;
                        }

                        int amount = dropItemAmountList[i];
                        for (int j = 0; j < amount; j++)
                        {
                            SpawnDropItem(dropItemData, monsterPosition);
                        }
                    }
                    break;
            }
        }
        
        public override void SpawnDropItemByMonsterType(MonsterController monster)
        {
            if (monster.IsDestroyed())
            {
                return;
            }
            
            MonsterType monsterType = monster.MonsterType;
            Vector3 monsterPosition = monster.Position;
            switch (monsterType)
            {
                case MonsterType.Normal:
                    float value = Random.value;
                    bool isPossibleSpawnDropItem = value > _waveData.nonDropRate;
                    if (!isPossibleSpawnDropItem)
                    {
                        return;
                    }

                    GemType gemType = GemType.None;
                    float purpleGemDropRate = _waveData.PurpleGemDropRate;
                    float greenGemRatio = _waveData.GreenGemDropRate;
                    float blueGemRatio = _waveData.BlueGemDropRate;
                    float redGemDropRate = _waveData.RedGemDropRate;
                    bool isSuccess = Utils.TrySpawnGem(ref gemType, purpleGemDropRate, greenGemRatio, blueGemRatio,
                        redGemDropRate);
                    
                    if (isSuccess)
                    {
                        GemController gem = Manager.I.Object.MakeGem(gemType, monsterPosition);
                        if (gem != null)
                        {
                            CurrentMapController.AddItemInGrid(gem.transform.position, gem);
                        }
                    }
                    break;
                case MonsterType.Boss:
                case MonsterType.Elite:
                    int targetWaveIndex = monster.SpawnedWaveIndex;
                    WaveData waveData = _stageData.WaveList.Find(v => v.WaveIndex == targetWaveIndex);
                    
                    if (waveData.EliteAndBossClearDropItemId == null ||
                        waveData.EliteAndBossClearDropItemAmount == null)
                    {
                        return;
                    }
                    
                    int idCount = waveData.EliteAndBossClearDropItemId.Count;
                    List<int> dropItemIdList = waveData.EliteAndBossClearDropItemId;
                    List<int> dropItemAmountList = waveData.EliteAndBossClearDropItemAmount;
                    for (int i = 0; i < idCount; i++)
                    {
                        int id = dropItemIdList[i];
                        if (!Manager.I.Data.DropItemDict.TryGetValue(id, out DropItemData dropItemData))
                        {
                            Debug.LogWarning($"failed spawn drop item {id}");
                            continue;
                        }

                        int amount = dropItemAmountList[i];
                        for (int j = 0; j < amount; j++)
                        {
                            SpawnDropItem(dropItemData, monsterPosition);
                        }
                    }
                    break;
            }
        }

        public override void ForceSpawnMonster(MonsterType monsterType)
        {
            if (monsterType == MonsterType.Normal)
            {
                List<int> monsterIdList = _waveData.MonsterId;
                int onceSpawnCount = _waveData.OnceSpawnCount;
                if (onceSpawnCount > 0)
                {
                    RaiseSpawnMonster(onceSpawnCount, monsterIdList, MonsterType.Normal);
                }
            }
        }

        public async UniTask SpawnMonsterAsync(WaveData waveData)
        {
            float spawnInterval = 1; //waveData.SpawnInterval;
            List<int> monsterIdList = waveData.MonsterId;
            int onceSpawnCount = waveData.OnceSpawnCount;
            List<int> eliteIdList = waveData.EliteId;
            List<int> bossIdList = waveData.BossId;
            float eliteSpawnTime = waveData.EliteSpawnTime;
            float elapsed = 0;
            bool isSpawnedElite = false;

            if (waveData.WaveType == WaveType.Boss)
            {
                if (bossIdList.Count > 0)
                {
                    RaiseSpawnMonster(bossIdList.Count, bossIdList, MonsterType.Boss);
                    Manager.I.Event.Raise(GameEventType.SpawnedBoss);
                    // _isStopTimer = true;
                }
                
                return;
            }

            // _isStopTimer = false;
            while (true)
            {
                try
                {
                    await UniTask.WaitForSeconds(spawnInterval, cancellationToken: _waveCts.Token);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    Debug.LogError($"{nameof(SpawnMonsterAsync)} error {e}");
                    return;
                }
                catch (Exception e) when(e is OperationCanceledException)
                {
                    return;
                }
                
                if (onceSpawnCount > 0)
                {
                    RaiseSpawnMonster(onceSpawnCount, monsterIdList, MonsterType.Normal);
                }

                if (elapsed > eliteSpawnTime 
                                && eliteIdList != null 
                                && eliteIdList.Count > 0
                                && !isSpawnedElite)
                {
                    isSpawnedElite = true;
                    RaiseSpawnMonster(eliteIdList.Count, eliteIdList, MonsterType.Elite);
                }

                elapsed += spawnInterval;
            }
        }

        public void ForceShutDownWave()
        {
            Utils.SafeCancelCancellationTokenSource(ref _waveCts);
        }

        public void Dispose()
        {
            Utils.SafeCancelCancellationTokenSource(ref _stageCts);
            Utils.SafeCancelCancellationTokenSource(ref _waveCts);
        }

        #region Cheat

        public void Cheat_CompletedStage(bool isCompleted)
        {
            foreach (WaveData waveData in _stageData.WaveList)
            {
                ReceiveRewardItemByWaveClear(waveData);
            }
            
            StopGame();
            if (isCompleted)
            {
                Manager.I.Event.Raise(GameEventType.CompletedStage);
            }
            else
            {
                GameManager gameManager = Manager.I.Game;
                StageBase currentStage = gameManager.CurrentStage;
                GameType gameType = gameManager.GameType;
                string stageResult = gameType == GameType.INFINITY
                    ? "InfinityMode"
                    : (currentStage as NormalStage)?.StageLevel.ToString();
                var presenter = PresenterFactory.CreateOrGet<GameOverPopupPresenter>();
                var stageModel = ModelFactory.CreateOrGetModel<StageModel>();
                presenter.OpenGameOverPopup(stageModel.ElapsedGameTime.Value, stageResult, gameType);
            }
        }
        
        #endregion
    }
}