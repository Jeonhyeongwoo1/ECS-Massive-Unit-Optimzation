using System;
using DG.Tweening;
using MewVivor.Common;
using MewVivor.Data;
using MewVivor.Enum;
using UnityEngine;

namespace MewVivor.InGame.Skill.SKillBehaviour
{
    public class SkillBehaviour_10051 : Projectile
    {
        [SerializeField] private GameObject _normalProjectileObject;
        [SerializeField] private GameObject _normalProjectileParticleObject;
        [SerializeField] private GameObject _ultimateProjectileParticleObject;

        private CreatureController _owner;
        private AttackSkillData _attackSkillData;
        private bool _isMaxLevel;
        private Unity.Entities.Entity _skillEntity;
        private Vector3 _direction;
        private float _projectileSpeed;
        private bool _isReceivedHitMonsterEntityEvent = false;

        public override void Generate(Transform targetTransform, Vector3 direction, AttackSkillData attackSkillData,
            CreatureController owner, int currentLevel)
        {
            transform.position = targetTransform.position;
            transform.localScale = Vector3.one * attackSkillData.Scale;
            _owner = owner;
            _attackSkillData = attackSkillData;
            _isReceivedHitMonsterEntityEvent = false;

            bool isMaxLevel = Const.MAX_AttackSKiLL_Level == currentLevel;
            _isMaxLevel = isMaxLevel;
            _normalProjectileObject.SetActive(true);
            _normalProjectileParticleObject.SetActive(false);
            _ultimateProjectileParticleObject.SetActive(false);
            // _rigidbody.simulated = true;
            gameObject.SetActive(true);

            var modifer = owner.SkillBook.GetPassiveSkillStatModifer(PassiveSkillType.ProjectileSpeed);
            float speed = Utils.CalculateStatValue(attackSkillData.ProjectileSpeed, modifer);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            // _rigidbody.SetRotation(angle);
            // _rigidbody.linearVelocity = direction * speed;

            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            _direction = direction;
            _projectileSpeed = speed;
            StartCoroutine(WaitDuration(Const.PROJECTILE_LIFE_CYCLE, Release));

            _skillEntity = CreateBaseSkillEntity(attackSkillData);
        }

        private void Update()
        {
            transform.Translate(_direction * (_projectileSpeed * Time.deltaTime), Space.World);
        }

        public override void OnHitMonsterEntity(Unity.Entities.Entity monsterEntity)
        {
            if (_isReceivedHitMonsterEntityEvent)
            {
                return;
            }

            _isReceivedHitMonsterEntityEvent = true;
            base.OnHitMonsterEntity(monsterEntity);

            StopAllCoroutines();
            var modifer = _owner.SkillBook.GetPassiveSkillStatModifer(PassiveSkillType.ExplsionRange);
            float attackRange = Utils.CalculateStatValue(_attackSkillData.AttackRange, modifer);
            float explosionSkillSize = Utils.GetPlayerStat(CreatureStatType.ExplosionSkillSize);
            attackRange *= explosionSkillSize;
            CreateExplosionSkillComponent(_skillEntity, attackRange);

            _normalProjectileParticleObject.SetActive(!_isMaxLevel);
            _ultimateProjectileParticleObject.SetActive(_isMaxLevel);
            _normalProjectileObject.SetActive(false);
            // _rigidbody.simulated = false;
            DOVirtual.DelayedCall(1, Release);
        }
    }
}