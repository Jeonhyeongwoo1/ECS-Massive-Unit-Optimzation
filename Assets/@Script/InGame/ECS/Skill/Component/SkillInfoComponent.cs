using System.Collections.Generic;
using Unity.Entities;


public struct SkillInfoComponent : IComponentData
{
    public float DamagePercent;
    public bool IsIntervalAttack;
    public int AttackCount;
    public float IntervalAttackTime;

    public float CurrentAttackCount;
    public float AttackElapsedTime;
}

