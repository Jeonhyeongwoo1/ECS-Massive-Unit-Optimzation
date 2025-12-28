using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MewVivor.Data;
using MewVivor.Data.Server;
using MewVivor.Enum;
using MewVivor.Factory;
using MewVivor.InGame.Controller;
using MewVivor.InGame.Entity;
using MewVivor.InGame.Stage;
using MewVivor.Managers;
using MewVivor.Model;
using MewVivor.Presenter;
using MewVivor.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MewVivor
{
    public partial class GameManager
    {
        public MapController CurrentMapController => _currentStage.CurrentMapController;
        public GameState GameState => _gameState;
        public GameType GameType => _gameType;
        public StageBase CurrentStage => _currentStage;

        public int StageLevel
        {
            get
            {
                if (_currentStage is NormalStage)
                {
                    return (_currentStage as NormalStage).StageLevel;
                }

                Debug.LogWarning("is not normal stage" + _gameType);
                return -1;
            }
        }
        
        private GameState _gameState;
        private StageBase _currentStage;
        private GameType _gameType;
        
        private readonly EventManager _event = Manager.I.Event;
        
        public void Initialize()
        {
            AddEvent();
        }

        ~GameManager()
        {
            RemoveEvent();
        }

        private void AddEvent()
        {
            _event.AddEvent(GameEventType.DeadPlayer, OnDeadPlayer);
            _event.AddEvent(GameEventType.CompletedStage, OnCompletedStage);
            _event.AddEvent(GameEventType.UseResurrection, OnResurrectionProcess);
            _event.AddEvent(GameEventType.ChangeGameType, OnChangeGameType);
            _event.AddEvent(GameEventType.GameOver,OnGameOver);
        }

        private void RemoveEvent()
        {
            _event.RemoveEvent(GameEventType.GameOver, OnGameOver);
            _event.RemoveEvent(GameEventType.DeadPlayer, OnDeadPlayer);
            _event.RemoveEvent(GameEventType.CompletedStage, OnCompletedStage);
            _event.RemoveEvent(GameEventType.UseResurrection, OnResurrectionProcess);
            _event.RemoveEvent(GameEventType.ChangeGameType, OnChangeGameType);
        }

        private async void OnGameOver(object value)
        {
            await OnGameOverAsync();
        }

        private void OnChangeGameType(object value)
        {
            _gameType = (GameType)value;
        }

        private void OnCompletedStage(object value)
        {
            var gameResultPopup = PresenterFactory.CreateOrGet<GameResultPopupPresenter>();
            string stageResult = _gameType == GameType.MAIN
                ? (_currentStage as NormalStage)?.StageLevel.ToString()
                : "InfiniteMode";

            StageModel stageModel = ModelFactory.CreateOrGetModel<StageModel>();
            gameResultPopup.OpenGameResultPopup(stageModel.ElapsedGameTime.Value, stageResult, _gameType);
        }

        private async void OnDeadPlayer(object value)
        {
            if (_gameState == GameState.DeadPlayer)
            {
                Debug.Log("Already dead player");
                return;
            }
            
            UpdateGameState(GameState.DeadPlayer);

            float interval = 2;
            await UniTask.WaitForSeconds(interval, cancelImmediately: true);
            if (SceneManager.GetActiveScene().name != SceneType.GameScene.ToString())
            {
                return;
            }
            
            PlayerModel model = ModelFactory.CreateOrGetModel<PlayerModel>();
            if (model.ResurrectionUseCount.Value >= Const.ResurrectionAvailableCount)
            {
                await OnGameOverAsync();
            }
            else
            {
                var resurrectionPopupPresenter = PresenterFactory.CreateOrGet<ResurrectionPopupPresenter>();
                resurrectionPopupPresenter.OpenResurrectionPopup();
            }
        }

        public async UniTask RequestGameEnd(GameEndType gameEndType)
        {
            var stageModel = ModelFactory.CreateOrGetModel<StageModel>();
            var playerModel = ModelFactory.CreateOrGetModel<PlayerModel>();
            TimeSpan playTimeSpan = TimeSpan.FromSeconds(stageModel.ElapsedGameTime.Value);
            GameEndRequestData gameEndRequestData = new GameEndRequestData
            {
                gameSessionId = playerModel.GameSessionId,
                clearedWave = stageModel.CurrentWaveStep.Value,
                normalMonsterKillCount = stageModel.NormalMonsterKillCount.Value,
                eliteMonsterKillCount = stageModel.EliteMonsterKillCount.Value,
                bossMonsterKillCount = stageModel.BossMonsterKillCount.Value,
                survivalTime = (int)playTimeSpan.TotalMilliseconds,
                dropItems = new Dictionary<string, int>()
                {
                    { Const.ID_GOLD.ToString(), playerModel.Gold.Value },
                    { Const.ID_JEWEL.ToString(), playerModel.Jewel.Value }
                }
            };

            var rewardItemDict = playerModel.GetAcquiredRewardItemDict();
            if (rewardItemDict != null)
            {
                foreach (var (key, value) in rewardItemDict)
                {
                    gameEndRequestData.dropItems.TryAdd(key.ToString(), value);
                }
            }

            var response =
                await Manager.I.Web.SendRequest<GameEndResponseData>($"/user-game/end/{gameEndType}", gameEndRequestData,
                    MethodType.POST.ToString());

            if (response.statusCode != (int)ServerStatusCodeType.Success)
            {
                Manager.I.ChangeTitleScene();
            }
        }

        private async UniTask OnGameOverAsync()
        {
            string stageResult = _gameType == GameType.INFINITY
                ? "InfinityMode"
                : (CurrentStage as NormalStage)?.StageLevel.ToString();
            var presenter = PresenterFactory.CreateOrGet<GameOverPopupPresenter>();
            var stageModel = ModelFactory.CreateOrGetModel<StageModel>();
            presenter.OpenGameOverPopup(stageModel.ElapsedGameTime.Value, stageResult, _gameType);
        }

        public void DeadMonster(MonsterDeadData monsterDeadData)
        {
            var model = ModelFactory.CreateOrGetModel<StageModel>();
            model.AddMonsterKillCount(monsterDeadData.MonsterType);
            _currentStage.OnDeadMonster(monsterDeadData);
        }
        
        public void DeadMonster(MonsterController monster)
        {
            var model = ModelFactory.CreateOrGetModel<StageModel>();
            model.AddMonsterKillCount(monster.MonsterType);
            _currentStage.OnDeadMonster(monster);
        }

        private void OnResurrectionProcess(object value)
        {
            UpdateGameState(GameState.Start);
            PlayerModel model = ModelFactory.CreateOrGetModel<PlayerModel>();
            model.ResurrectionUseCount.Value++;
        }

        public async void GameEnd()
        {
            Debug.Log("GameEnd");
            TimeScaleHandler.Reset();
            UpdateGameState(GameState.Done);
            _currentStage.StopGame();
            Manager.I.Object.GameEnd();
            var playerModel = ModelFactory.CreateOrGetModel<PlayerModel>();
            playerModel.Reset();
            var stageModel = ModelFactory.CreateOrGetModel<StageModel>();
            stageModel.Reset();
            await Manager.I.MoveToLobbyScene();
        }

        public void SpawnDropItem(DropItemData dropItemData, Vector3 spawnPosition, bool useRandomSpawnPosition = true)
        {
            _currentStage.SpawnDropItem(dropItemData, spawnPosition, useRandomSpawnPosition);
        }
        
        public void StartGame(GameType gameType, int stageIndex)
        {
            if (gameType == GameType.NONE)
            {
                Debug.LogError("Select gameType : " + _gameType);
                return;
            }
            
            switch (gameType)
            {
                case GameType.MAIN:
                    StageData stageData = Manager.I.Data.StageDict[stageIndex];
                    _currentStage = new NormalStage(stageData, 0);
                    break;
                case GameType.INFINITY:
                    _currentStage = new InfinityStage();
                    break;
            }

            _gameType = gameType;
            TimeScaleHandler.Reset();
            _currentStage.StartGame();
            UpdateGameState(GameState.Start);
            _event.Raise(GameEventType.GameStart);
            Manager.I.Object.StartGame();
        }

        public void UpdateGameState(GameState gameState) => _gameState = gameState;

        public (float, float) GetMonsterAtkAndHP(MonsterType monsterType, CreatureData creatureData)
        {
            float finalHP = 0;
            float finalAtk = 0;
            switch (_gameType)
            {
                case GameType.MAIN:
                    //Hp StageLevel * 기본 체력 * (HpRate + HpIncreaseRate)
                    //Atk StageLevel * 기본 공격력 * (AtkRate + AtkIncreaseRate)
                    var normalStage = CurrentStage as NormalStage;
                    float atkRate = 1;
                    float hpRate = 1;
                    switch(monsterType)
                    {
                        case MonsterType.Normal:
                        case MonsterType.SuicideBomber:
                        case MonsterType.Boss:
                            atkRate = normalStage.CurrentWaveData.AtkRate;
                            hpRate = normalStage.CurrentWaveData.HpRate;
                            // int stageLevel = StageLevel;
                            break;
                        case MonsterType.Elite:
                            atkRate = normalStage.CurrentWaveData.EliteAtkRate;
                            hpRate = normalStage.CurrentWaveData.EliteHpRate;
                            // stageLevel = StageLevel;
                            break;
                    }

                    // finalHP = stageLevel * creatureData.MaxHp * hpRate;
                    // finalAtk = stageLevel * creatureData.Atk * atkRate;
                    finalHP =  creatureData.MaxHp * hpRate;
                    finalAtk =  creatureData.Atk * atkRate;
                    break;
                case GameType.INFINITY:
                    DataManager dataManager = Manager.I.Data;
                    InfinityStage stage = CurrentStage as InfinityStage;
                    float upRate = 0;
                    float defaultAtk = 0;
                    float defaultHp = 0;
                    switch (monsterType)
                    {
                        case MonsterType.Normal:
                            upRate = stage.NormalEnemyStatUpRate;
                            defaultAtk =
                                dataManager.InfiniteModeConfigDataDict[InfiniteModeConfigName.NormalEnemyDefaultAttack].Value;
                            defaultHp = 
                                dataManager.InfiniteModeConfigDataDict[InfiniteModeConfigName.NormalEnemyDefaultHP].Value;
                            break;
                        case MonsterType.Elite:
                            upRate = stage.EliteEnemyStatUpRate;
                            defaultAtk =
                                dataManager.InfiniteModeConfigDataDict[InfiniteModeConfigName.EliteEnemyDefaultAttack].Value;
                            defaultHp = 
                                dataManager.InfiniteModeConfigDataDict[InfiniteModeConfigName.EliteEnemyDefaultHP].Value;
                            break;
                        case MonsterType.Boss:
                            upRate = stage.BossStatUpRate;
                            defaultAtk =
                                dataManager.InfiniteModeConfigDataDict[InfiniteModeConfigName.BossEnemyDefaultAttack].Value;
                            defaultHp = 
                                dataManager.InfiniteModeConfigDataDict[InfiniteModeConfigName.BossEnemyDefaultHP].Value;
                            break;
                    }
                
                    finalHP = defaultHp * (1 + upRate);
                    finalAtk = defaultAtk * (1 + upRate);
                    break;
            }

            return (finalAtk, finalHP);
        }
        
        #region PathFinding

        private readonly Vector3Int[] _dirArray =
        {
            new Vector3Int(1, 0),
            new Vector3Int(0, 1),
            new Vector3Int(-1, 0),
            new Vector3Int(0, -1),
            new Vector3Int(1, 1),
            new Vector3Int(1, -1),
            new Vector3Int(-1, 1),
            new Vector3Int(-1, -1),
        };

        private Dictionary<Vector3Int, CreatureController> _cellDict = new Dictionary<Vector3Int, CreatureController>();
        
        public List<Vector3Int> PathFinding(Vector3Int startPosition, Vector3Int destPosition, int maxDepth = 10)
        {
            // 상하좌우 + 대각선 (8 방향)
            //int[] cost = { 10, 10, 10, 10, 14, 14, 14, 14 }; // 대각선 이동은 비용 14

            Dictionary<Vector3Int, int> bestDict = new Dictionary<Vector3Int, int>();
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
            PriorityQueue<Node> queue = new PriorityQueue<Node>();
            Dictionary<Vector3Int, Vector3Int> pathDict = new Dictionary<Vector3Int, Vector3Int>();

            //목적지에 도착하지 못할 경우에 그나마 가장 가까운 위치로 보낸다.
            int closedH = int.MaxValue;
            Vector3Int closePos = Vector3Int.zero;
            int depth = 0;

            {
                int h = Mathf.Abs(destPosition.x - startPosition.x) + Mathf.Abs(destPosition.y - startPosition.y);
                Node startNode = new Node(h, depth, startPosition);
                queue.Push(startNode);
                bestDict[startPosition] = h;
                pathDict[startPosition] = startPosition;

                closedH = h;
                closePos = startPosition;
            }

            while (!queue.IsEmpty())
            {
                Node node = queue.Top();
                queue.Pop();

                Vector3Int nodePos = new Vector3Int(node.x, node.y, 0);
                if (nodePos == destPosition)
                {
                    // closePos = nodePos;
                    break;
                }

                if (visited.Contains(nodePos))
                {
                    continue;
                }
                
                visited.Add(nodePos);
                depth = node.depth;

                // Debug.Log($"{depth}");
                if (depth == maxDepth)
                {
                    break;
                }
                
                for (int i = 0; i < _dirArray.Length; i++)
                {
                    int nextY = node.y + _dirArray[i].y;
                    int nextX = node.x + _dirArray[i].x;
                    if (!CanGo(nextX, nextY))
                    {
                        continue;
                    }

                    // Debug.Log("Can");
                    Vector3Int nextPos = new Vector3Int(nextX, nextY, 0);
                    //int g = cost[i] + node.g;
                    int h = Mathf.Abs(destPosition.x - nextPos.x) + Mathf.Abs(destPosition.y - nextPos.y);
                    if (bestDict.ContainsKey(nextPos) && bestDict[nextPos] <= h)
                    {
                        continue;
                    }

                    bestDict[nextPos] = h;
                    queue.Push(new Node(h, depth + 1, nextPos));
                    pathDict[nextPos] = nodePos;
                    
                    if (closedH > h)
                    {
                        closedH = h;
                        closePos = nextPos;
                    }
                }
            }

            List<Vector3Int> list = new List<Vector3Int>();
            Vector3Int now = destPosition;

            if (!pathDict.ContainsKey(now))
            {
                now = closePos;
                if (!pathDict.ContainsKey(now))
                {
                    Debug.LogError($"Pathfinding Error: No valid path found to {destPosition} or closest position {closePos}");
                    return new List<Vector3Int>(); 
                }
            }

            int count = 0;
            while (pathDict[now] != now)
            {
                if (!list.Contains(now))
                {
                    list.Add(now);
                }
            
                count++;
                now = pathDict[now];
                if (count > maxDepth)
                {
                    break;
                }
            }

            list.Add(now);
            list.Reverse();
            // Debug.Log($"now {depth} {now} / {destPosition} / {closePos} / {startPosition} / {list.Count}");
            return list;
        }

        public bool CanGo(int x, int y, bool ignoreObject = false)
        {
            if (ignoreObject)
            {
                return true;
            }
            
            if (_cellDict.TryGetValue(new Vector3Int(x, y), out var creature))
            {
                return false;
            }
            
            return true;
        }

        public void AddCreatureInGrid(Vector3Int position, CreatureController creature)
        {
            if (_cellDict.ContainsKey(position))
            {
                return;
            }
            
            _cellDict[position] = creature;
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            return Vector3Int.zero;
            // return _currentMap.Grid.Grid.WorldToCell(worldPosition);
        }

        public Vector3 CellToWorld(Vector3Int cellPosition)
        {
            return _currentStage.CurrentMapController.Grid.Grid.CellToWorld(cellPosition);
        }
        
        #endregion
    }
}