using MewVivor.Data;
using MewVivor.InGame.Skill;
using Unity.Entities;
using Unity.Transforms;

public partial class SkillSpawnSystem : SystemBase
{
    protected override void OnUpdate()
    {

    }

    //모든 스킬의 Base 엔티티
    public Entity CreateBaseSkillEntity(Projectile projectile, AttackSkillData attackSkillData, bool isIntervalAttack,
        float intervalAttackTime = 0, int attackCount = 1)
    {
        if (!SystemAPI.ManagedAPI.TryGetSingleton<SkillSpawnPrefabData>(out var skillSpawnPrefabData))
        {
            return default;
        }

        var skillEntityData =
            skillSpawnPrefabData.SkillPrefabList.Find(v => v.AttackSkillType == attackSkillData.AttackSkillType);
        Entity skillEntity = EntityManager.Instantiate(skillEntityData.Entity);
        EntityManager.AddComponentData(skillEntity, new SkillBridgeComponentData()
        {
            BaseSkillData = attackSkillData,
            Projectile = projectile
        });

        EntityManager.AddComponentData(skillEntity, new SkillInfoComponent()
        {
            DamagePercent = attackSkillData.DamagePercent,
            IsIntervalAttack = isIntervalAttack,
            AttackCount = attackCount,
            IntervalAttackTime = intervalAttackTime,
            CurrentAttackCount = 0,
            AttackElapsedTime = 0
        });

        EntityManager.AddBuffer<SkillHitEntityBufferData>(skillEntity);
        var skillTransform = SystemAPI.GetComponent<LocalTransform>(skillEntity);
        skillTransform.Position = projectile.transform.position;
        skillTransform.Rotation = projectile.transform.rotation;
        skillTransform.Scale = attackSkillData.Scale;
        EntityManager.SetComponentData(skillEntity, skillTransform);
        return skillEntity;
    }

    public Entity CreateExplosionSkill(Entity skillEntity, float attackRange)
    {
        var singleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = singleton.CreateCommandBuffer(World.Unmanaged);
        ecb.AddComponent(skillEntity, new SkillRangeAttackComponentData()
        {
            Range = attackRange
        });

        return skillEntity;
    }
}