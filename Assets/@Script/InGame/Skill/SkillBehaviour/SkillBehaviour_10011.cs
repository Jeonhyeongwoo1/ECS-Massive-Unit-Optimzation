using System;
using DG.Tweening;
using MewVivor.Common;
using MewVivor.Data;
using MewVivor.Enum;
using UnityEngine;

namespace MewVivor.InGame.Skill.SKillBehaviour
{
    public class SkillBehaviour_10011 : Projectile
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        private Camera _camera;
        private Vector3 _direction;
        private float _projectileSpeed;
        
        public override void Generate(Transform targetTransform, Vector3 direction,
                                            AttackSkillData attackSkillData, CreatureController owner, int currentLevel)
        {
            _direction = direction;

            if (!string.IsNullOrEmpty(attackSkillData.SkillSprite))
            {
                Sprite sprite = Manager.I.Resource.Load<Sprite>(attackSkillData.SkillSprite);
                _spriteRenderer.sprite = sprite;
            }
            
            var modifer = owner.SkillBook.GetPassiveSkillStatModifer(PassiveSkillType.ProjectileSpeed);
            _projectileSpeed = Utils.CalculateStatValue(attackSkillData.ProjectileSpeed, modifer);
            transform.localScale = Vector3.one * attackSkillData.Scale;
            transform.position = targetTransform.position;
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
            bool reflected = false;
            if (viewportPos.x < 0)
            {
                _direction = Vector3.Reflect(_direction, Vector3.right);
                reflected = true;
            }
            else if (viewportPos.x > 1)
            {
                _direction = Vector3.Reflect(_direction, Vector3.left);
                reflected = true;
            }

            if (viewportPos.y < 0)
            {
                _direction = Vector3.Reflect(_direction, Vector3.up);
                reflected = true;
            }
            else if (viewportPos.y > 1)
            {
                _direction = Vector3.Reflect(_direction, Vector3.down);
                reflected = true;
            }
            
            // 충돌 후 화면 안쪽으로 위치 클램프
            if (reflected)
            {
                var clampedPos = new Vector3(
                    Mathf.Clamp01(viewportPos.x),
                    Mathf.Clamp01(viewportPos.y),
                    viewportPos.z);

                transform.position = _camera.ViewportToWorldPoint(clampedPos);
            }

            // float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            // _rigidbody.SetRotation(angle);
            
          transform.Translate(_direction  * (_projectileSpeed * Time.deltaTime), Space.World);
            // _rigidbody.linearVelocity = _direction * _projectileSpeed;
        }

        public override void Release()
        {
            transform.DOScale(0, 0.15f).OnComplete(() =>
            {
                transform.localScale = Vector3.one;
                base.Release();
            });
        }
    }
}