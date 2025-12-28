using MewVivor.Data;
using MewVivor.Enum;
using MewVivor.InGame.Controller;
using UnityEngine;

namespace MewVivor.InGame.Skill.SKillBehaviour
{
    public class SkillBehaviour_10061 : Projectile
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private SectorGizmoDrawer _sectorGizmoDrawer;

        private Unity.Entities.Entity _skillEntity;
        
        public override void Generate(Transform targetTransform, Vector3 direction, AttackSkillData attackSkillData,
            CreatureController owner, int currentLevel)
        {
            gameObject.SetActive(true);
            if (attackSkillData.SkillCategoryType == SkillCategoryType.Ultimate)
            {
                _animator.Play("Skill_10061_ultimate");
                transform.localPosition = Vector3.zero;
            }
            else
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);

                float radius = attackSkillData.AttackRange;
                transform.rotation = rotation;
                transform.position = targetTransform.position + direction * -1 ;
            
                Vector3 rotatedDirection = rotation * Vector3.right;
                _animator.Play("Skill_10061");
                _sectorGizmoDrawer.SetData(attackSkillData.ConeAngle, radius, rotatedDirection);
            }
            
            transform.localScale = Vector3.one * attackSkillData.Scale;
            _skillEntity = CreateBaseSkillEntity(attackSkillData);
            UseSKill(direction, currentLevel == Const.MAX_AttackSKiLL_Level, attackSkillData);
        }

        private void UseSKill(Vector3 direction, bool isMaxLevel, AttackSkillData attackSkillData)
        {
            PlayerController player = Manager.I.Object.Player;
            float ratio = Random.value;
            float damage = 0;
            bool isCritical = false;
            float angle = isMaxLevel ? 360 : attackSkillData.ConeAngle;
            if (ratio < player.CriticalPercent.Value)
            {
                damage *= player.CriticalDamagePercent.Value;
                isCritical = true;
            }
            
            Manager.I.Object.AttackMonsterAndBossEntityListInFanShape(_skillEntity, 
                damage, 
                isCritical, 
                transform.position,
                direction * -1,
                attackSkillData.AttackRange,
                angle);
        }
        
        public override void Release()
        {
            gameObject.SetActive(false);
        }
    }
}