using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MewVivor.Common;
using MewVivor.Data;
using MewVivor.Enum;
using MewVivor.InGame.Controller;
using MewVivor.InGame.Enum;
using MewVivor.InGame.Stat;
using MewVivor.Key;
using UnityEngine;

namespace MewVivor.InGame.Skill
{
    /// <summary>
    /// 스킬 설명 : 플레이어가 바라보는 방향으로 부채꼴 모양으로 공격
    /// </summary>
    public class Skill_10001 : RepeatAttackSkill
    {
        private SectorGizmoDrawer _sectorGizmoDrawer;
        
        protected override async UniTask UseSkill()
        {
            int count = AttackSkillData.NumOfProjectile;
            Manager.I.Audio.Play(Sound.SFX, SoundKey.UseSkill_10001);
            for (int i = 0; i < count; i++)
            {
                var player = _owner as PlayerController;
                GameObject prefab = Manager.I.Resource.Instantiate(AttackSkillData.PrefabLabel);
                IGeneratable generatable = prefab.GetComponent<IGeneratable>();
            
                ObjectManager objectManager = Manager.I.Object;
                // List<MonsterController> monsterList = objectManager.GetNearestMonsterList();
                var monsterList = objectManager.GetNearestMonsterPositionList();
                Vector3 direction = _owner.GetDirection();
                if (monsterList != null && monsterList.Count > 0)
                {
                    direction = (monsterList[0] - _owner.transform.position).normalized;
                }
            
                generatable.OnHit = OnHit;
                generatable.Generate(player.AttackPoint, direction, AttackSkillData, _owner, CurrentLevel);
            
                try
                {
                    await UniTask.WaitForSeconds(AttackSkillData.ProjectileSpacing,
                        cancellationToken: _skillLogicCts.Token);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    Debug.LogError($"error {nameof(UseSkill)} log : {e.Message}");
                    StopSkillLogic();
                }    
            }
        }

        public override void StopSkillLogic()
        {
            Utils.SafeCancelCancellationTokenSource(ref _skillLogicCts);
            Release();
        }

        protected override float GetDamage()
        {
            float damage = _owner.Atk.Value;
            if (AttackSkillData.DamagePercent > 0)
            {
                damage *= AttackSkillData.DamagePercent;
            }

            StatModifer statModifer = _owner.SkillBook.GetPassiveSkillStatModifer(PassiveSkillType.Attack);
            float finalDamage = Utils.CalculateStatValue(damage, statModifer);
            return finalDamage;
        }

        protected override float CalculateCoolTime()
        {
            float coolTime = base.CalculateCoolTime();
            float value = Utils.GetPlayerStat(CreatureStatType.BasicSkillCoolTime);
            float ratio = Mathf.Max(0.1f, (1 - value));
            return coolTime * ratio;
        }
        
        protected override void Hit(Transform target, Projectile projectile)
        {
            if (Utils.TryGetComponentInParent(target.gameObject, out IHitable hitable))
            {
                if (hitable is MonsterController)
                {
                    if (AttackSkillData.KnockbackDistance != 0)
                    {
                        var monster = hitable as MonsterController;
                        monster.ExecuteKnockback(AttackSkillData.KnockbackDistance);
                    }
                
                    if (AttackSkillData.DebuffType1 != DebuffType.None)
                    {
                        var monster = hitable as MonsterController;
                        monster.ApplyDebuff(AttackSkillData.DebuffType1, AttackSkillData.DebuffValue1,
                            AttackSkillData.DebuffValuePercent1, AttackSkillData.DebuffDuration1);
                    }

                    if (AttackSkillData.DebuffType2 != DebuffType.None)
                    {
                        var monster = hitable as MonsterController;
                        monster.ApplyDebuff(AttackSkillData.DebuffType2, AttackSkillData.DebuffValue2,
                            AttackSkillData.DebuffValuePercent2, AttackSkillData.DebuffDuration2);
                    }
                }
                
                float damage = GetDamage();
                // damage = 10;
                hitable.TakeDamage(damage, _owner);
                AccumulatedDamage += damage;
                
                if (hitable is MonsterController)
                {
                    var monster = hitable as MonsterController;
                    if (!monster.IsDead)
                    {
                        return;
                    }
                    
                    PlayerController player = _owner as PlayerController;
                    if (player.BombStat == null)
                    {
                        return;
                    }
                    
                    float bombDamage = player.BombStat.Value;
                    EvolutionData bombEvolutionData = Manager.I.Data.EvolutionDataDict[EvolutionOrderType.Evolution_Boom.ToString()];
                    float radius = bombEvolutionData.DefaultEvolutionValue1;

                    var prefab = Manager.I.Resource.Instantiate(Const.Explosion_Name);
                    prefab.transform.position = monster.Position;
                    prefab.gameObject.SetActive(true);
                    Collider2D[] collider2Ds = Physics2D.OverlapCircleAll(monster.Position, radius,
                        LayerMask.GetMask("Monster", "Boss"));
                    foreach (Collider2D collider2D in collider2Ds)
                    {
                        if (collider2D == null)
                        {
                            continue;
                        }

                        if (Utils.TryGetComponentInParent(target.gameObject, out IHitable hitMonster))
                        {
                            hitMonster.TakeDamage(bombDamage, _owner);
                        }
                    }

                    DOVirtual.DelayedCall(1,
                        () => Manager.I.Pool.ReleaseObject(Const.Explosion_Name, prefab.gameObject));
                }
            }
        }
    }
}