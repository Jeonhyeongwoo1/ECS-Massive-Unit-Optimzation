using System;
using MewVivor.Common;
using MewVivor.Data;
using MewVivor.Enum;
using UnityEngine;

namespace MewVivor.InGame.Skill.SKillBehaviour
{
    public class SkillBehaviour_10041 : Projectile
    {
        private Camera _camera;
        private Vector3 _direction;
        private float _projectileSpeed;
        
        public override void Generate(Transform targetTransform, Vector3 direction, AttackSkillData attackSkillData, CreatureController owner, int currentLevel)
        {
            _direction = direction;
            
            var modifer = owner.SkillBook.GetPassiveSkillStatModifer(PassiveSkillType.ProjectileSpeed);
            _projectileSpeed = Utils.CalculateStatValue(attackSkillData.ProjectileSpeed, modifer);
            transform.position = targetTransform.position;
            transform.localScale = Vector3.one * attackSkillData.Scale;
            gameObject.SetActive(true);
            
            CreateBaseSkillEntity(attackSkillData);
        }

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void FixedUpdate()
        {
            if (_camera == null)
            {
                return;
            }
            
            var viewportPos = _camera.WorldToViewportPoint(transform.position);
            if (viewportPos.x < 0 || viewportPos.x > 1 || viewportPos.y < 0 || viewportPos.y > 1)
            {
                Release();
                return;
            }
            
            // float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            // _rigidbody.SetRotation(angle);
            // _rigidbody.linearVelocity = _direction * _projectileSpeed;
            transform.Translate(_direction  * (_projectileSpeed * Time.deltaTime), Space.World);
        }
    }
}