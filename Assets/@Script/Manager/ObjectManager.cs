using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MewVivor.Common;
using MewVivor.Data;
using MewVivor.Enum;
using MewVivor.InGame;
using MewVivor.InGame.Controller;
using MewVivor.InGame.Entity;
using MewVivor.InGame.Enum;
using MewVivor.InGame.View;
using MewVivor.Key;
using MewVivor.Managers;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI.Extensions;
using Random = UnityEngine.Random;

namespace MewVivor
{
    public class ObjectManager
    {
        public int ActivateMonsterCount => ActivateMonsterList?.Count ?? 0;
        public List<MonsterController> ActivateMonsterList => _activateMonsterList;
        public List<DropItemController> DroppedItemControllerList => _droppedItemControllerList;
        public PlayerController Player => _player;

        protected Camera Camera
        {
            get
            {
                if (_camera == null)
                {
                    _camera = Camera.main;
                }

                return _camera;
            }
        }
        
        private ResourcesManager _resource;
        private DataManager _data;
        private PoolManager _pool;
        private EventManager _event;
        private Camera _camera;

        private List<MonsterController> _activateMonsterList;
        private List<DropItemController> _droppedItemControllerList = new();
        
        private PlayerController _player;
        private BossBarrierController _bossBarrier;

        private EntityManager _entityManager;
        private EntityQuery _monsterEntityQuery;
        private NativeArray<Entity> _monsterEntityArray => _monsterEntityQuery.ToEntityArray(Allocator.Temp);

        private NativeArray<LocalTransform> _monsterLocalTransfromArray
        {
            get
            {
                if (_monsterEntityQuery == default || _monsterEntityQuery.IsEmpty)
                {
                    return default;
                }
                
                var data =
                    _monsterEntityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                return data;
            }
        }
        
        public void Initialize()
        {
            Manager manager = Manager.I;
            _resource = manager.Resource;
            _data = manager.Data;
            _pool = manager.Pool;
            _event = manager.Event;
            _camera = Camera.main;
            
            AddEvent();
        }

        ~ObjectManager()
        {
            RemoveEvent();    
        }

        public void StartGame()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _monsterEntityQuery = _entityManager.CreateEntityQuery(typeof(MonsterComponent), typeof(LocalTransform));
        }

        public void GameEnd()
        {
            _activateMonsterList.Clear();
            _droppedItemControllerList.Clear();
            _player = null;
        }

        public void CreatePlayer()
        {
            CreatureData creatureData = _data.CreatureDict[(int)CreatureType.Player];
            List<AttackSkillData> skillDataList = creatureData.SkillTypeList.Select(i => _data.AttackSkillDict[i]).ToList();
            
            GameObject playerPrefab = Manager.I.Resource.Instantiate("Player");
            _player = Utils.AddOrGetComponent<PlayerController>(playerPrefab);
            _player.Initialize(creatureData, skillDataList);
        }

        private void AddEvent()
        {
            _event.AddEvent(GameEventType.ActivateDropItem, OnActivateDropItem);
            _event.AddEvent(GameEventType.CompletedStage, OnCompletedStage);
            _event.AddEvent(GameEventType.UseResurrection, OnResurrectionProcess);
        }

        private void RemoveEvent()
        {
            _event.RemoveEvent(GameEventType.ActivateDropItem, OnActivateDropItem);
            _event.RemoveEvent(GameEventType.CompletedStage, OnCompletedStage);
            _event.RemoveEvent(GameEventType.UseResurrection, OnResurrectionProcess);
        }

        private void OnResurrectionProcess(object value)
        {
            bool isResurrectionByEquipment = (bool)value;
            if (!isResurrectionByEquipment)
            {
                RemoveMonster(MonsterType.Normal, true);
            }

            var gameScene = Manager.I.UI.SceneUI as UI_GameScene;
            gameScene.ShowWhiteFlash();
            OnResurrectionPlayerAsync().Forget();
        }

        private async UniTask OnResurrectionPlayerAsync()
        {
            try
            {
                await _player.DoResurrectionAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"error {e.Message}");
                Manager.I.MoveToLobbyScene();
                return;
            }
        }

        private void OnCompletedStage(object value)
        {
            AllObjectRelease();
        }

        public void ReleaseMonster(MonsterController monster)
        {
            _activateMonsterList.Remove(monster);
            _pool.ReleaseObject(monster.PrefabLabel, monster.gameObject);
        }

        public void DeadMonster(MonsterDeadData monsterDeadData)
        {
            Manager.I.Game.DeadMonster(monsterDeadData);
            
            switch (monsterDeadData.MonsterType)
            {
                case MonsterType.Normal:
                    break;
                case MonsterType.Elite:
                    break;
                case MonsterType.Boss:
                    var uiGameScene = Manager.I.UI.SceneUI as UI_GameScene;
                    if (uiGameScene != null)
                    {
                        uiGameScene.HideMonsterInfo(monsterDeadData.MonsterType);
                    }
                    
                    if (_bossBarrier != null)
                    {
                        _bossBarrier.Release();
                    }
                    
                    Manager.I.Game.CurrentStage.OnBossKill();
                    break;
            }
        }

        public void DeadMonster(MonsterController monster)
        {
            Manager.I.Game.DeadMonster(monster);
            
            _activateMonsterList.Remove(monster);
            _pool.ReleaseObject(monster.PrefabLabel, monster.gameObject);
            switch (monster.MonsterType)
            {
                case MonsterType.Normal:
                    break;
                case MonsterType.Elite:
                    break;
                case MonsterType.Boss:
                    var uiGameScene = Manager.I.UI.SceneUI as UI_GameScene;
                    if (uiGameScene != null)
                    {
                        uiGameScene.HideMonsterInfo(monster.MonsterType);
                    }
                    
                    if (_bossBarrier != null)
                    {
                        _bossBarrier.Release();
                    }
                    
                    Manager.I.Game.CurrentStage.OnBossKill();
                    break;
            }
        }

        private void OnActivateDropItem(object value)
        {
            DropItemController item = (DropItemController)value;
            DropItemData dropItemData = item.DropItemData;

            _droppedItemControllerList.Remove(item);
            switch (dropItemData.DropItemType)
            {
                case DropableItemType.Magnet:
                    var itemList = _droppedItemControllerList
                        .Where(x => x.DropableItemType == DropableItemType.Gem)
                        .ToList();
                    
                    itemList.ForEach(v =>
                    {
                        void Callback()
                        {
                            var gem = v as GemController;
                            int exp = gem.GetExp();
                            Player.CurrentExp = exp;
                        }

                        v.GetItem(_player.transform, true, Callback);
                    });
                    Manager.I.Game.CurrentMapController.Grid.RemoveAllItem(DropableItemType.Gem);
                    break;
                case DropableItemType.Bomb:
                    float range = dropItemData.Value;
                    float dist = range * range;
                    Vector3 playerPos = _player.Position;
                    foreach (MonsterController monster in _activateMonsterList)
                    {
                        if ((monster.Position - playerPos).sqrMagnitude > dist)
                        {
                            continue;
                        }

                        monster.TakeBombSkill();
                    }

                    if (dropItemData.DataId == Const.ID_NORMAL_BOMB)
                    {
                        Manager.I.Game.CurrentStage.ForceSpawnMonster(MonsterType.Normal);
                    }
                    break;
            }
        }

        public void RemoveDropItem(DropItemController dropItemController)
        {
            _droppedItemControllerList.Remove(dropItemController);
        }

        public bool isSpawnMonster = true;

        public MonsterController SpawnMonster(int monsterId, Vector3 spawnPosition, MonsterType monsterType, int waveIndex)
        {
            if (!isSpawnMonster)
            {
                return null;
            }

            GlobalConfigData monsterLimitCount = _data.GlobalConfigDataDict[GlobalConfigName.MonsterCountLimit];
            if (monsterLimitCount.Value < ActivateMonsterCount && monsterType == MonsterType.Normal)
            {
                Debug.Log($"monster is full / limit {monsterLimitCount.Value} / currentCount {ActivateMonsterCount}");
                return null;
            }

            CreatureData data = _data.CreatureDict[monsterId];

            return null;
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var requestEntity = entityManager.CreateEntity();
            spawnPosition += _player.Position;
            entityManager.AddComponentData(requestEntity, new MonsterSpawnRequestComponent()
            {
                Count = 1,
                PlayerPosition = new float3(spawnPosition.x, spawnPosition.y, spawnPosition.z),
                Scale = 2.5f,
                Speed = 2,
                Radius = 2,
                MaxHP = 130,
                Atk = data.Atk,
                MonsterType = monsterType,
                SpawnedWaveIndex = waveIndex
            });

            switch (monsterType)
            {
                case MonsterType.Boss:
                    var uiGameScene = Manager.I.UI.SceneUI as UI_GameScene;
                    uiGameScene.ShowMonsterInfo(MonsterType.Boss, data.DescriptionTextID, 1);
                    var prefab = _resource.Instantiate("BossBarrierController");
                    var bossBarrierController = prefab.GetComponent<BossBarrierController>();
                    bossBarrierController.Initialize(BarrierType.Circle);
                    _bossBarrier = bossBarrierController;
                    RemoveMonster(MonsterType.Normal, false);
                    break;
            }
            
            return null;
            
            GameObject monsterObj = _resource.Instantiate(data.PrefabLabel);
            var monster = Utils.AddOrGetComponent<MonsterController>(monsterObj);

            List<AttackSkillData> skillDataList = data.SkillTypeList?.Select(i => _data.AttackSkillDict[i]).ToList();
            monster.Initialize(data, skillDataList);

            switch (monster.MonsterType)
            {
                case MonsterType.SuicideBomber:
                case MonsterType.Normal:
                    spawnPosition += _player.Position;
                    break;
                case MonsterType.Boss:
                    var uiGameScene = Manager.I.UI.SceneUI as UI_GameScene;
                    uiGameScene.ShowMonsterInfo(MonsterType.Boss, data.DescriptionTextID, 1);
                    var prefab = _resource.Instantiate("BossBarrierController");
                    var bossBarrierController = prefab.GetComponent<BossBarrierController>();
                    bossBarrierController.Initialize(BarrierType.Circle);
                    _bossBarrier = bossBarrierController;
                    RemoveMonster(MonsterType.Normal, false);
                    break;
            }
            
            _activateMonsterList.Add(monster);
            monster.Spawn(spawnPosition, _player, waveIndex);

            return monster;
        }

        private void RemoveMonster(MonsterType monsterType, bool forceKill)
        {
            for (int i = _activateMonsterList.Count - 1; i >= 0; i--)
            {
                MonsterController monster = _activateMonsterList[i];
                if (monster.MonsterType == monsterType)
                {
                    if (forceKill)
                    {
                        monster.ForceKill();
                    }
                    else
                    {
                        monster.Release();
                    }
                    
                    _activateMonsterList.RemoveAt(i);
                }
            }
        }
        
        public GemController MakeGem(GemType gemType, Vector3 spawnPosition)
        {
            if (Const.DropItemMaxCount <= _droppedItemControllerList.Count)
            {
                return null;
            }
            
            GameObject prefab = _resource.Instantiate(Const.ExpGem);
            Sprite sprite = _resource.Load<Sprite>(gemType.ToString());
            var gem = prefab.GetOrAddComponent<GemController>();
            gem.SetGemInfo(gemType, sprite);
            gem.Spawn(spawnPosition, null);
            
            _droppedItemControllerList.Add(gem);
            return gem;
        }

        public bool IsAliveBossMonster()
        {
            MonsterController monster = _activateMonsterList.Find(v => v.MonsterType == MonsterType.Boss);
            return monster != null;
        }

        public void AllObjectRelease()
        {
            for (int i = _droppedItemControllerList.Count - 1; i >= 0; i--)
            {
                _droppedItemControllerList[i]?.Release();
            }

            for (int i = _activateMonsterList.Count - 1; i >= 0; i--)
            {
                _activateMonsterList[i]?.Release();
            }

            _player?.Release();
        }
        
        public void ShowDamageFont(Vector2 pos, float damage, float healAmount, Transform parent, bool isCritical = false)
        {
            // string prefabName;
            // if (isCritical)
            //     prefabName = "CriticalDamageFont";
            // else
            //     prefabName = "DamageFont";
            //
            // GameObject go = _resource.Instantiate(prefabName);
            // DamageFont damageText = go.GetOrAddComponent<DamageFont>();

            var damageText = Manager.I.UI.UIFontObject.GetDamageFont(isCritical);
            damageText?.SetInfo(pos, damage, healAmount, parent, isCritical);
        }

        public List<MonsterController> GetMonsterInRange(float minDistance, float maxDistance, Vector3 targetPosition)
        {
            List<MonsterController> monsterList =
                _activateMonsterList.OrderBy(a => (targetPosition - a.transform.position).sqrMagnitude).ToList();

            if (monsterList.Count == 0)
            {
                return null;
            }
         
            var list = new List<MonsterController>();   
            foreach (MonsterController monster in monsterList)
            {
                float distance = Vector3.Distance(targetPosition, monster.Position);
                if (distance > maxDistance)
                {
                    break;
                }
                
                if (minDistance <= distance && distance <= maxDistance)
                {
                    list.Add(monster);
                }
            }

            return list;
        }

        public List<MonsterController> GetMonsterList(int count = 1)
        {
            return _activateMonsterList.Take(count).ToList();
        }

        public bool IsInCameraView(Vector3 worldPosition)
        {
            Vector3 viewportPos = Camera.WorldToViewportPoint(worldPosition);
            return viewportPos.x >= 0 && viewportPos.x <= 1 &&
                   viewportPos.y >= 0 && viewportPos.y <= 1;
        }
        
        
        #region Entity

        public List<Vector3> GetNearestMonsterPositionList(int count = 1, float minDistance = 0f)
        {
            if (Manager.I.Game.GameState == GameState.Done)
            {
                return null;
            }

            Vector3 playerPos = _player.Position;
            float minDistanceSqr = minDistance * minDistance;
            var monsterArray = _monsterLocalTransfromArray
                .Where(m => (playerPos - m.Position.ToVector3()).sqrMagnitude >= minDistanceSqr)
                .OrderBy(m => (playerPos - m.Position.ToVector3()).sqrMagnitude)
                .Select(x => x.Position.ToVector3())
                .ToList();
            
            int min = math.min(count, monsterArray.Count);
            if (min == 0)
            {
                return null;
            }

            monsterArray = monsterArray.Take(min).ToList();
            return monsterArray;
        }

        public void AttackMonsterAndBossEntityListInFanShape(Entity skillEntity, float damage, bool isCritical,
            Vector3 position, Vector3 direction, float radius,
            float angle = 180)
        {
            var skillHitSystemBase = _entityManager.World.GetOrCreateSystemManaged<SkillHitSystemBase>();
            skillHitSystemBase.AttackMonsterAndBossEntityListInFanShape(skillEntity, damage, isCritical, position,
                direction, radius, angle);
        }

        #endregion
       
        public List<MonsterController> GetNearestMonsterList(int count = 1, float minDistance = 0f)
        {
            if (Manager.I.Game.GameState == GameState.Done)
            {
                return null;
            }
            
            Vector3 playerPos = _player.Position;
            float minDistanceSqr = minDistance * minDistance;

            // 1. 최소 거리 필터링 + 2. 가까운 순으로 정렬
            List<MonsterController> monsterList = _activateMonsterList
                .Where(m => (playerPos - m.transform.position).sqrMagnitude >= minDistanceSqr)
                .OrderBy(m => (playerPos - m.transform.position).sqrMagnitude)
                .ToList();
            
            int min = math.min(count, monsterList.Count);
            if (min == 0)
            {
                return null;
            }

            monsterList = monsterList.Take(min).ToList();

            //카운터가 몬스터 리스트보다 크면 몬스터를 더 추가해준다.
            // while (count > monsterList.Count)
            // {
            //     monsterList.Add(monsterList[Random.Range(0, monsterList.Count)]);
            // }

            return monsterList;
        }

        public List<Transform> GetMonsterInCameraArea(int count = 1)
        {
            _cachedMonsterList.Clear();
            foreach (MonsterController monster in _activateMonsterList)
            {
                Vector3 monsterPos = monster.transform.position;
                Vector3 viewportPoint = Camera.WorldToViewportPoint(monsterPos);
                if (viewportPoint.x < 0f || viewportPoint.x > 1f
                                         || viewportPoint.y < 0f || viewportPoint.y > 1f)
                {
                    continue;
                }
                
                _cachedMonsterList.Add(monster.transform);
                if (_cachedMonsterList.Count >= count)
                {
                    break;
                }
            }

            return _cachedMonsterList;
        }
        
        public Vector3 GetRandomPositionInCameraArea(float min = 0.2f, float max = 0.8f)
        {
            return Camera.ViewportToWorldPoint(
                    new Vector3(Random.Range(min, max), Random.Range(min, max), 0));
        }
        
        public Vector2 GetRandomPositionOutsideCamera(float distanceFromEdge = 2f)
        {
            Camera cam = Camera;

            // 카메라의 뷰포트는 (0,0) ~ (1,1)
            // 외부는 -distance ~ 0 또는 1 ~ 1+distance 범위로 확장
            float x = 0, y = 0;

            // 랜덤 방향 결정: 0=상, 1=하, 2=좌, 3=우
            int side = UnityEngine.Random.Range(0, 4);

            switch (side)
            {
                case 0: // 위쪽
                    x = UnityEngine.Random.Range(-0.5f, 1.5f);
                    y = 1 + distanceFromEdge;
                    break;
                case 1: // 아래쪽
                    x = UnityEngine.Random.Range(-0.5f, 1.5f);
                    y = -distanceFromEdge;
                    break;
                case 2: // 왼쪽
                    x = -distanceFromEdge;
                    y = UnityEngine.Random.Range(-0.5f, 1.5f);
                    break;
                case 3: // 오른쪽
                    x = 1 + distanceFromEdge;
                    y = UnityEngine.Random.Range(-0.5f, 1.5f);
                    break;
            }

            // 뷰포트 좌표를 월드 좌표로 변환
            Vector3 viewportPoint = new Vector3(x, y, cam.nearClipPlane + 5f); // Z는 거리
            Vector3 worldPosition = cam.ViewportToWorldPoint(viewportPoint);

            return worldPosition;
        }
        
        

        private Collider2D[] _collider2D = new Collider2D[100];
        private List<Transform> _cachedMonsterList = new();
        
        public List<Transform> GetMonsterAndBossTransformListInFanShape(Transform target, Vector3 direction, float radius, float angle = 180)
        {
            return GetMonsterAndBossTransformListInFanShape(target.position, direction, radius, angle);
        }

        public List<Transform> GetMonsterAndBossTransformListInFanShape(Vector3 position, Vector3 direction, float radius, float angle = 180)
        {
            _cachedMonsterList.Clear();
            int count = Physics2D.OverlapCircleNonAlloc(position, radius, _collider2D,
                Layer.AttackableLayer);
            
            for (int i = 0; i < count; i++)
            {
                var collider = _collider2D[i];
                // 정확한 중심 좌표 계산
                var circle = collider as CircleCollider2D;
                Vector3 monsterCenter = circle != null
                    ? circle.transform.TransformPoint(circle.offset)
                    : collider.transform.position;

                Vector3 inVector = (monsterCenter - position).normalized;

                // 안정적인 각도 계산
                float dot = Vector3.Dot(inVector, direction);
                dot = Mathf.Clamp(dot, -1f, 1f); // 중요!
                float degree = Mathf.Acos(dot) * Mathf.Rad2Deg;

                if (degree <= angle / 2)
                {
                    _cachedMonsterList.Add(collider.transform);
                }
            }
            
            if (_cachedMonsterList.Count == 0)
            {
                return null;
            }
            
            List<Transform> monsterList =
                _cachedMonsterList.OrderBy(a => (_player.Position - a.transform.position).sqrMagnitude).ToList();
            return monsterList;
        }

        private Dictionary<int, List<MonsterController>> _quadAreaDict = new();
        
        public List<Transform> GetCenterMonsterInCameraArea(int count = 1)
        {
            _quadAreaDict.Clear();
            foreach (MonsterController monster in _activateMonsterList)
            {
                Vector3 monsterPos = monster.transform.position;
                Vector3 viewportPoint = Camera.WorldToViewportPoint(monsterPos);

                if (viewportPoint.x < 0f || viewportPoint.x > 1f || viewportPoint.y < 0f || viewportPoint.y > 1f)
                {
                    continue;
                }

                int area = (viewportPoint.x < 0.5f ? (viewportPoint.y > 0.5f ? 1 : 3) : (viewportPoint.y > 0.5f ? 2 : 4));
                AddQuadAreaDict(monster, area);
            }
            
            var sortedAreas = _quadAreaDict
                .OrderByDescending(kv => kv.Value.Count)
                .Take(Mathf.Clamp(count, 1, 4)) // count만큼 영역 선택
                .ToList();
            
            List<Transform> list = new ();
            foreach (var (key, monsterList) in sortedAreas)
            {
                Vector3 centerPosition = Vector3.zero;
                foreach (MonsterController monsterController in monsterList)
                {
                    centerPosition += monsterController.transform.position;
                }
                
                centerPosition /= monsterList.Count;
                
                Transform closest = null;
                float minDistance = Mathf.Infinity;

                foreach (var monster in monsterList)
                {
                    float distance = Vector3.SqrMagnitude(monster.transform.position - centerPosition);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closest = monster.transform;
                    }
                }

                if (closest != null)
                {
                    list.Add(closest);
                }
            }

            return list.Count == 0 ? null : list;
        }

        private void AddQuadAreaDict(MonsterController monster, int area)
        {
            if (!_quadAreaDict.ContainsKey(area))
            {
                _quadAreaDict[area] = new List<MonsterController>();
            }
            
            List<MonsterController> list = _quadAreaDict[area];
            list.Add(monster);
            _quadAreaDict[area] = list;
        }
    }
}
