using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MewVivor.Common;
using MewVivor.Data;
using MewVivor.Enum;
using MewVivor.Factory;
using MewVivor.InGame.Controller;
using MewVivor.InGame.Entity;
using MewVivor.Model;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MewVivor.InGame.Stage
{
    public class InfinityStage : StageBase
    {
        public float NormalEnemyStatUpRate => _normalEnemyStatUpRate * (_stageModel.CurrentWaveStep.Value - 1);
        public float EliteEnemyStatUpRate => _eliteEnemyStatUpRate * _stageModel.EliteMonsterKillCount.Value;
        public float BossStatUpRate => _bossStatUpRate * _stageModel.BossMonsterKillCount.Value;

        private Dictionary<InfiniteModeConfigName, float> _configDataDict;
        private int _firstEnemySpawnCount;
        private float _normalEnemyStatUpRate;
        private float _eliteEnemyStatUpRate;
        private float _bossStatUpRate;
        private WaveType _waveType;
        private List<int> _cachedEliteMonsterIdList = new();
        private List<int> _cachedBossMonsterIdList = new();
        private List<int> _cachedNormalMonsterIdList = new();

        private List<int> _currentSpawnedNormalMonsterIdList = new();
        private List<int> _currentSpawnedEliteMonsterIdList = new();
        private List<int> _currentSpawnedBossMonsterIdList = new();
        private int _normalMonsterIdIndex;
        private int _eliteMonsterIdIndex;
        private int _bossMonsterIdIndex;

        public InfinityStage()
        {
            _stageModel = ModelFactory.CreateOrGetModel<StageModel>();
            _stageModel.CurrentWaveStep.Value = 1;
            _waveType = WaveType.Normal;

            Dictionary<InfiniteModeConfigName, InfiniteModeConfigData> dict = Manager.I.Data.InfiniteModeConfigDataDict;
            _configDataDict = new Dictionary<InfiniteModeConfigName, float>(dict.Count);
            foreach (var (key, value) in dict)
            {
                _configDataDict[key] = value.Value;
            }

            string MapName = "Map_01";
            //맵, 몬스터 스폰, Wave
            GameObject map = _resource.Instantiate(MapName, false);
            _currentMapController = map.GetComponent<MapController>();
            _object.CreatePlayer();

            _firstEnemySpawnCount = (int)_configDataDict[InfiniteModeConfigName.FirstEnemySpawnCount];
            _normalEnemyStatUpRate = _configDataDict[InfiniteModeConfigName.NormalEnemyStatUpRate];
            _eliteEnemyStatUpRate = _configDataDict[InfiniteModeConfigName.EliteEnemyStatUpRate];
            _bossStatUpRate = _configDataDict[InfiniteModeConfigName.BossStatUpRate];
        }

        public override void StartGame()
        {
            base.StartGame();

            StartStageAsync().Forget();
        }

        private async UniTask StartStageAsync()
        {
            GameStartTime = DateTime.UtcNow;
            while (true)
            {
                try
                {
                    await StartWave();
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Debug.LogError($"Error {nameof(StartStageAsync)} / {e.Message}");
                    //Go Title
                    return;
                }
                catch (Exception e) when (e is OperationCanceledException)
                {
                    break;
                }
            }
        }

        protected override async UniTask StartWave()
        {
            await base.StartWave();
            Dictionary<int, CreatureData> creatureDict = Manager.I.Data.CreatureDict;
            _cachedEliteMonsterIdList = creatureDict.Values
                .Where(x => x.PrefabLabel == "ElitePrefab")
                .Select(x => x.DataId)
                .ToList();
            _cachedBossMonsterIdList = creatureDict.Values
                .Where(x => x.PrefabLabel == "BossPrefab")
                .Select(x => x.DataId)
                .ToList();
            _cachedNormalMonsterIdList = creatureDict.Values
                .Where(x => x.PrefabLabel == "MonsterPrefab")
                .Select(x => x.DataId)
                .ToList();
            
            _currentSpawnedNormalMonsterIdList = new List<int>() { _cachedNormalMonsterIdList[_normalMonsterIdIndex] };
            _currentSpawnedEliteMonsterIdList = new List<int>() { _cachedEliteMonsterIdList[_eliteMonsterIdIndex] };
            _currentSpawnedBossMonsterIdList = new List<int>() { _cachedBossMonsterIdList[_bossMonsterIdIndex] };
            float waveTime = _configDataDict[InfiniteModeConfigName.OneWaveTime];
            await UniTask.WhenAll(SpawnMonsterAsync(), IncreasedDifficultyCycleAsync(), WaveTimerAsync((int)waveTime));
        }

        public override void OnDeadMonster(MonsterController monster)
        {
            if (monster.MonsterType == MonsterType.Boss)
            {
                _waveType = WaveType.Normal;
                _bossMonsterIdIndex = (_bossMonsterIdIndex + 1) % _cachedBossMonsterIdList.Count;
                _currentSpawnedBossMonsterIdList = new List<int>() { _cachedBossMonsterIdList[_bossMonsterIdIndex] };
            }
            else if (monster.MonsterType == MonsterType.Elite)
            {
                _eliteMonsterIdIndex = (_eliteMonsterIdIndex + 1) % _cachedEliteMonsterIdList.Count;
                _currentSpawnedEliteMonsterIdList = new List<int>() { _cachedEliteMonsterIdList[_eliteMonsterIdIndex] };
            }

            base.OnDeadMonster(monster);
        }

        protected override async UniTask WaveTimerAsync(int remainTime)
        {
            while (_waveCts != null && !_waveCts.IsCancellationRequested)
            {
                try
                {
                    await base.WaveTimerAsync(remainTime);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Debug.LogError($"Failed error {e.Message}");
                    return;
                }
                
                //Wave done
                _stageModel.CurrentWaveStep.Value++;

                _normalMonsterIdIndex = (_normalMonsterIdIndex + 1) % _cachedNormalMonsterIdList.Count;
                _currentSpawnedNormalMonsterIdList = new List<int>()
                    { _cachedNormalMonsterIdList[_normalMonsterIdIndex] };
            }
        }

        private async UniTask IncreasedDifficultyCycleAsync()
        {
            while (_waveCts != null && !_waveCts.IsCancellationRequested)
            {
                float increasedDifficultyCycle = _configDataDict[InfiniteModeConfigName.IncreasedDifficultyCycle];

                try
                {
                    await UniTask.WaitForSeconds(increasedDifficultyCycle, cancellationToken: _waveCts.Token);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Debug.LogError($"Failed error {e.Message}");
                    return;
                }

                float upValue = _configDataDict[InfiniteModeConfigName.IncreasedDifficultyStatValue];
                //유닛 공격력 및 스폰 갯수 증가
                // int origin = _firstEnemySpawnCount;
                _firstEnemySpawnCount *= (int)upValue;
                _normalEnemyStatUpRate *= upValue;
                _eliteEnemyStatUpRate *= upValue;
                _bossStatUpRate *= upValue;
            }
        }

        public override void OnBossKill()
        {
            _waveType = WaveType.Normal;
        }

        //TODO : 추가하기
        public override void ForceSpawnMonster(MonsterType monsterType)
        {
            if (monsterType == MonsterType.Normal)
            {
                // SpawnMonster(normalEnemySpawnCount, normalMonsterId, MonsterType.Normal);
            }
        }

        public async UniTask SpawnMonsterAsync()
        {
            int normalEnemySpawnCount = _firstEnemySpawnCount;
            int eliteEnemySpawnCount = 1;
            int bossEnemySpawnCount = 1;

            float eliteEnemyOnceSpawnTime = _configDataDict[InfiniteModeConfigName.EliteEnemyOnceSpawnTime];
            float bossEnemyOnceSpawnTime = _configDataDict[InfiniteModeConfigName.BossEnemyOnceSpawnTime];
            float normalEnemyOnceSpawnTime = _configDataDict[InfiniteModeConfigName.NormalEnemyOnceSpawnTime];
            float normalEnemySpawnUpCount = _configDataDict[InfiniteModeConfigName.NormalEnemySpawnUpCount];

            float normalEnemySpawnElapsed = 0;
            float eliteEnemySpawnElapsed = 0;
            float bossEnemySpawnElapsed = 0;

            int timeInterval = 1;
            while (_waveCts != null && !_waveCts.IsCancellationRequested)
            {
                while (_waveType == WaveType.Boss)
                {
                    try
                    {
                        await UniTask.Yield(_waveCts.Token);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Debug.LogError($"Error {nameof(SpawnMonsterAsync)} / {e.Message}");
                        return;
                    }
                }

                if (normalEnemySpawnElapsed > normalEnemyOnceSpawnTime)
                {
                    normalEnemySpawnElapsed = 0;
                    SpawnMonster(normalEnemySpawnCount, _currentSpawnedNormalMonsterIdList, MonsterType.Normal);
                    normalEnemySpawnCount += (int)normalEnemySpawnUpCount;
                }

                if (eliteEnemySpawnElapsed > eliteEnemyOnceSpawnTime)
                {
                    eliteEnemySpawnElapsed = 0;
                    SpawnMonster(eliteEnemySpawnCount, _currentSpawnedEliteMonsterIdList, MonsterType.Elite);
                }

                if (bossEnemySpawnElapsed > bossEnemyOnceSpawnTime)
                {
                    bossEnemySpawnElapsed = 0;
                    SpawnMonster(bossEnemySpawnCount, _currentSpawnedBossMonsterIdList, MonsterType.Boss);
                    _waveType = WaveType.Boss;
                    Manager.I.Event.Raise(GameEventType.SpawnedBoss);
                }

                try
                {
                    await UniTask.WaitForSeconds(timeInterval, cancellationToken: _waveCts.Token);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Debug.LogError($"Error {nameof(SpawnMonsterAsync)} / {e.Message}");
                    return;
                }

                normalEnemySpawnElapsed += timeInterval;
                eliteEnemySpawnElapsed += timeInterval;
                bossEnemySpawnElapsed += timeInterval;
            }
        }

        private void SpawnMonster(int spawnCount, List<int> monsterId, MonsterType monsterType)
        {
            RaiseSpawnMonster(spawnCount, monsterId, monsterType);
        }

        protected override void ReceiveRewardItemByMonsterKill(MonsterDeadData monsterDeadData)
        {
            MonsterType monsterType = monsterDeadData.MonsterType;
            var stageModel = ModelFactory.CreateOrGetModel<StageModel>();
            var playerModel = ModelFactory.CreateOrGetModel<PlayerModel>();

            int killCount = GetKillCount(stageModel, monsterType);
            var config = GetRewardConfig(monsterType);

            if (killCount > 0 && killCount % config.KillThreshold == 0)
            {
                if (!playerModel.AcquiredRewardItemDict.ContainsKey(config.RewardItemId))
                {
                    playerModel.AcquiredRewardItemDict[config.RewardItemId] = 0;
                }

                playerModel.AcquiredRewardItemDict[config.RewardItemId] += config.RewardAmount;
            }

            if (monsterType == MonsterType.Boss)
            {
                float prob = _configDataDict[InfiniteModeConfigName.RandomRewardProbForBossKill];
                int rewardId = (int)_configDataDict[InfiniteModeConfigName.RandomRewardIdForBossKill];
                int minRange = (int)_configDataDict[InfiniteModeConfigName.RandomRewardMinAmountForBossKill];
                int maxRange = (int)_configDataDict[InfiniteModeConfigName.RandomRewardMaxAmountForBossKill];
                GetRewardByBossKill(prob, rewardId, minRange, maxRange);
            }
        }

        protected override void ReceiveRewardItemByMonsterKill(MonsterController monster)
        {
            MonsterType monsterType = monster.MonsterType;
            var stageModel = ModelFactory.CreateOrGetModel<StageModel>();
            var playerModel = ModelFactory.CreateOrGetModel<PlayerModel>();

            int killCount = GetKillCount(stageModel, monsterType);
            var config = GetRewardConfig(monsterType);

            if (killCount > 0 && killCount % config.KillThreshold == 0)
            {
                if (!playerModel.AcquiredRewardItemDict.ContainsKey(config.RewardItemId))
                {
                    playerModel.AcquiredRewardItemDict[config.RewardItemId] = 0;
                }

                playerModel.AcquiredRewardItemDict[config.RewardItemId] += config.RewardAmount;
            }

            if (monsterType == MonsterType.Boss)
            {
                float prob = _configDataDict[InfiniteModeConfigName.RandomRewardProbForBossKill];
                int rewardId = (int)_configDataDict[InfiniteModeConfigName.RandomRewardIdForBossKill];
                int minRange = (int)_configDataDict[InfiniteModeConfigName.RandomRewardMinAmountForBossKill];
                int maxRange = (int)_configDataDict[InfiniteModeConfigName.RandomRewardMaxAmountForBossKill];
                GetRewardByBossKill(prob, rewardId, minRange, maxRange);
            }
        }

        private (int KillThreshold, int RewardItemId, int RewardAmount) GetRewardConfig(MonsterType monsterType)
        {
            return monsterType switch
            {
                MonsterType.Normal => (
                    (int)_configDataDict[InfiniteModeConfigName.RewardForNormalEnemyKillCount],
                    (int)_configDataDict[InfiniteModeConfigName.RewardItemIdForNormalKill],
                    (int)_configDataDict[InfiniteModeConfigName.RewardItemAmountForNormalKill]
                ),
                MonsterType.Elite => (
                    (int)_configDataDict[InfiniteModeConfigName.RewardForEliteEnemyKillCount],
                    (int)_configDataDict[InfiniteModeConfigName.RewardItemIdForEliteKill],
                    (int)_configDataDict[InfiniteModeConfigName.RewardItemAmountForEliteKill]
                ),
                MonsterType.Boss => (
                    (int)_configDataDict[InfiniteModeConfigName.RewardForBossEnemyKillCount],
                    (int)_configDataDict[InfiniteModeConfigName.RewardItemIdForBossKill],
                    (int)_configDataDict[InfiniteModeConfigName.RewardItemAmountForBossKill]
                ),
                _ => (0, 0, 0)
            };
        }

        public override void SpawnDropItemByMonsterType(MonsterDeadData monsterDeadData)
        {
            MonsterType monsterType = monsterDeadData.MonsterType;
            Vector3 monsterPosition = monsterDeadData.Position;
            switch (monsterType)
            {
                case MonsterType.Normal:
                    //Gem
                    GemType gemType = GemType.None;
                    float smallGemRatio = _configDataDict[InfiniteModeConfigName.PurpleGemDropRate];
                    float greenGemRatio = _configDataDict[InfiniteModeConfigName.GreenGemDropRate];
                    float blueGemRatio = _configDataDict[InfiniteModeConfigName.BlueGemDropRate];
                    float yellowGemRatio = _configDataDict[InfiniteModeConfigName.RedGemDropRate];
                    bool isSuccess = Utils.TrySpawnGem(ref gemType, smallGemRatio, greenGemRatio, blueGemRatio,
                        yellowGemRatio);

                    if (isSuccess)
                    {
                        GemController gem = Manager.I.Object.MakeGem(gemType, monsterPosition);
                        if (gem != null)
                        {
                            CurrentMapController.AddItemInGrid(gem.transform.position, gem);
                        }
                    }

                    //SkillUp
                    // if (Random.value < _configDataDict[InfiniteModeConfigName.SkillUpItemDropProb])
                    // {
                    //     int id = Const.ID_SKILLUP;
                    //     if (!Manager.I.Data.DropItemDict.TryGetValue(id, out DropItemData dropItemData))
                    //     {
                    //         Debug.LogWarning($"failed spawn drop item {id}");
                    //         return;
                    //     }
                    //
                    //     SpawnDropItem(dropItemData, monsterPosition);
                    // }

                    break;
                case MonsterType.Boss:
                    SpawnDropItem(monsterPosition);
                    break;
                case MonsterType.Elite:
                    if (Manager.I.Data.DropItemDict.TryGetValue(Const.ID_MAGENTIC, out DropItemData dropItemData))
                    {
                        SpawnDropItem(dropItemData, monsterPosition);
                    }
                    break;
            }
        }

        public override void SpawnDropItemByMonsterType(MonsterController monster)
        {
            MonsterType monsterType = monster.MonsterType;
            Vector3 monsterPosition = monster.Position;
            switch (monsterType)
            {
                case MonsterType.Normal:
                    //Gem
                    GemType gemType = GemType.None;
                    float smallGemRatio = _configDataDict[InfiniteModeConfigName.PurpleGemDropRate];
                    float greenGemRatio = _configDataDict[InfiniteModeConfigName.GreenGemDropRate];
                    float blueGemRatio = _configDataDict[InfiniteModeConfigName.BlueGemDropRate];
                    float yellowGemRatio = _configDataDict[InfiniteModeConfigName.RedGemDropRate];
                    bool isSuccess = Utils.TrySpawnGem(ref gemType, smallGemRatio, greenGemRatio, blueGemRatio,
                        yellowGemRatio);

                    if (isSuccess)
                    {
                        GemController gem = Manager.I.Object.MakeGem(gemType, monsterPosition);
                        if (gem != null)
                        {
                            CurrentMapController.AddItemInGrid(gem.transform.position, gem);
                        }
                    }

                    //SkillUp
                    // if (Random.value < _configDataDict[InfiniteModeConfigName.SkillUpItemDropProb])
                    // {
                    //     int id = Const.ID_SKILLUP;
                    //     if (!Manager.I.Data.DropItemDict.TryGetValue(id, out DropItemData dropItemData))
                    //     {
                    //         Debug.LogWarning($"failed spawn drop item {id}");
                    //         return;
                    //     }
                    //
                    //     SpawnDropItem(dropItemData, monsterPosition);
                    // }

                    break;
                case MonsterType.Boss:
                    SpawnDropItem(monsterPosition);
                    break;
                case MonsterType.Elite:
                    if (Manager.I.Data.DropItemDict.TryGetValue(Const.ID_MAGENTIC, out DropItemData dropItemData))
                    {
                        SpawnDropItem(dropItemData, monsterPosition);
                    }
                    break;
            }
        }

        private void SpawnDropItem(Vector3 spawnPosition)
        {
            int dropItemId = (int)_configDataDict[InfiniteModeConfigName.BossAndEliteClearDropItemId];
            if (!Manager.I.Data.DropItemDict.TryGetValue(dropItemId, out DropItemData dropItemData))
            {
                Debug.LogWarning($"failed spawn drop item {dropItemId}");
            }

            int amount = (int)_configDataDict[InfiniteModeConfigName.BossAndEliteClearDropItemAmount];
            for (int i = 0; i < amount; i++)
            {
                SpawnDropItem(dropItemData, spawnPosition);
            }
        }
    }
}