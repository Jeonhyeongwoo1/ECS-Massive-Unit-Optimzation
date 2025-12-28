using MewVivor.Data;
using MewVivor.InGame.Skill;
using Unity.Entities;
using UnityEngine;

public class SkillBridgeComponentData : IComponentData
{
    public Projectile Projectile;
    public BaseSkillData BaseSkillData;
}

public struct SkillTag : IComponentData
{
    
}
