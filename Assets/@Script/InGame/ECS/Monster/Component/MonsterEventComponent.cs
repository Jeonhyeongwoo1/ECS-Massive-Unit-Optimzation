using Unity.Entities;

public struct MonsterAttackEventComponent : IComponentData
{
    public Entity MonsterEntity;
}

public struct MonsterTakeDamagedEventComponent : IComponentData
{
    public Entity MonsterEntity;
    public Entity SkillEntity;
    public float Damage;
    public bool IsCritical;
}

public struct TriggerManageComponent : IComponentData
{
}