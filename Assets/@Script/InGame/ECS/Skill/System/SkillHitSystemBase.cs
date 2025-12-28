using Cysharp.Threading.Tasks;
using MewVivor;
using MewVivor.Enum;
using MewVivor.Key;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct MonsterDeadData
{
    public Vector3 Position;
    public MonsterType MonsterType;
    public int SpawnedWaveIndex;
}

public partial class SkillHitSystemBase : SystemBase
{
    protected override void OnUpdate()
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);
        var skillInfoComponentLookup = SystemAPI.GetComponentLookup<SkillInfoComponent>();
        
        foreach (var (damageEvent, eventEntity)
                 in SystemAPI.Query<RefRO<MonsterTakeDamagedEventComponent>>()
                     .WithEntityAccess())
        {
            if (eventEntity == Entity.Null
                || damageEvent.ValueRO.MonsterEntity == Entity.Null
                || !SystemAPI.Exists(damageEvent.ValueRO.MonsterEntity))
            {
                continue;
            }

            var skillEntity = damageEvent.ValueRO.SkillEntity;
            // var skillInfoComponent = skillInfoComponentLookup[skillEntity];
            // if (skillInfoComponent.IntervalAttackTime > skillInfoComponent.AttackElapsedTime)
            // {
            //     skillInfoComponent.AttackElapsedTime += SystemAPI.Time.DeltaTime;
            //     skillInfoComponentLookup[skillEntity] = skillInfoComponent;
            //     return;
            // }
            // else
            // {
            //     if (!skillInfoComponent.IsIntervalAttack)
            //     {
            //         skillInfoComponent.CurrentAttackCount++;
            //     }
            //     
            //     skillInfoComponent.AttackElapsedTime = 0;
            //     skillInfoComponentLookup[skillEntity] = skillInfoComponent;
            // }

            Entity targetMonster = damageEvent.ValueRO.MonsterEntity;
            float damage = damageEvent.ValueRO.Damage;
            damage = 30;
            if (SystemAPI.HasComponent<MonsterComponent>(targetMonster))
            {
                var monsterRef = SystemAPI.GetComponentRW<MonsterComponent>(targetMonster);
                monsterRef.ValueRW.CurrentHP -= damage;
                var monsterTransform = SystemAPI.GetComponentRO<LocalTransform>(targetMonster);

                if (monsterRef.ValueRW.CurrentHP <= 0)
                {
                    ecb.DestroyEntity(targetMonster);
                    var monsterDeadData = new MonsterDeadData
                    {
                        Position = monsterTransform.ValueRO.Position,
                        MonsterType = monsterRef.ValueRO.MonsterType,
                        SpawnedWaveIndex = monsterRef.ValueRO.SpawnedWaveIndex
                    };

                    Manager.I.Object.DeadMonster(monsterDeadData);
                }
                else
                {
                    var position = monsterTransform.ValueRO.Position;
                    var fontPosition = position + new float3(0, 1.5f, 0);
                    Manager.I.Object.ShowDamageFont(new float2(fontPosition.x, fontPosition.y),
                        damage,
                        0,
                        null,
                        damageEvent.ValueRO.IsCritical);

                    if (Manager.I.Object.IsInCameraView(position))
                    {
                        Manager.I.Audio.Play(Sound.SFX, SoundKey.MonsterHit, 0.5f, 0.15f).Forget();
                    }
                }
                
                var skillBridgeComponentData =
                    SystemAPI.ManagedAPI.GetComponent<SkillBridgeComponentData>(skillEntity);
                skillBridgeComponentData.Projectile.OnHitMonsterEntity(damageEvent.ValueRO.MonsterEntity);
            }

            ecb.DestroyEntity(eventEntity);
        }
    }

    public void AttackMonsterAndBossEntityListInFanShape(Entity skillEntity, float damage, bool isCritical,
        Vector3 position, Vector3 direction, float radius,
        float angle = 180)
    {
        var singleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = singleton.CreateCommandBuffer(World.Unmanaged);
        NativeList<Entity> list = GetMonsterAndBossEntityListInFanShape(position, direction, radius, angle);
        if (list.Length > 0)
        {
            foreach (Entity monsterEntity in list)
            {
                var monsterTakeDamagedEventComponent = new MonsterTakeDamagedEventComponent
                {
                    MonsterEntity = monsterEntity,
                    SkillEntity = skillEntity,
                    Damage = damage,
                    IsCritical = isCritical
                };

                Entity monsterTakeDamagedEntity = ecb.CreateEntity();
                ecb.AddComponent(monsterTakeDamagedEntity, monsterTakeDamagedEventComponent);
            }
        }

        list.Dispose();
    }

    public NativeList<Entity> GetMonsterAndBossEntityListInFanShape(Vector3 position, Vector3 direction, float radius,
        float angle = 180)
    {
        var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var collisionWorld = physicsWorldSingleton.CollisionWorld;
        var collisionFilter = ECSExtensions.CreateMonsterCollisionFilter();
        var hits = new NativeList<DistanceHit>(Allocator.Temp);
        var entityList = new NativeList<Entity>(Allocator.Temp);
        if (collisionWorld.OverlapSphere(position, radius, ref hits, collisionFilter))
        {
            if (hits.Length > 0)
            {
                foreach (DistanceHit hit in hits)
                {
                    float3 monsterPosition = hit.Position;
                    Vector3 inVector = (monsterPosition.ToVector3() - position).normalized;
                    float dot = Vector3.Dot(inVector, direction);
                    dot = Mathf.Clamp(dot, -1f, 1f);
                    float degree = Mathf.Acos(dot) * Mathf.Rad2Deg;
                    if (degree <= angle / 2)
                    {
                        entityList.Add(hit.Entity);
                    }
                }
            }
        }

        hits.Dispose();
        return entityList;
    }
}