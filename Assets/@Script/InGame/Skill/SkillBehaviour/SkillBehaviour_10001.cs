using System;
using System.Collections;
using DG.Tweening;
using MewVivor.Data;
using MewVivor.Enum;
using Unity.Entities;
using UnityEngine;

namespace MewVivor.InGame.Skill.SKillBehaviour
{
    public class SkillBehaviour_10001 : Projectile
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _ultimateSprite;
        [SerializeField] private SpriteRenderer _sprite;
        
        private Camera _camera;
        private Vector3 _dir;
        private AttackSkillData _attackSkillData;

        private void Start()
        {
            _camera = Camera.main;
        }

        private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            // 너무 작은 입력이면 fallback 사용
            if (v.sqrMagnitude < 1e-6f) return fallback;
            return v.normalized;
        }

        public override void Generate(Transform targetTransform, Vector3 direction,
            AttackSkillData attackSkillData, CreatureController owner, int currentLevel)
        {
            // 위치는 AttackPoint(= targetTransform) 기준이 자연스럽습니다.
            transform.position = targetTransform != null ? targetTransform.position : owner.Position;

            // 항상 정규화(입력이 0에 가까우면 owner의 바라보는 방향/오른쪽 등으로 대체)
            Vector3 dir = SafeNormalize(direction, owner.transform.right);

            // 회전도 정규화된 방향으로 계산 (안전)
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.localScale = Vector3.one * attackSkillData.Scale;

            bool isMaxLevel = currentLevel == Const.MAX_AttackSKiLL_Level;
            _sprite.sprite = isMaxLevel ? _ultimateSprite : _normalSprite;

            _attackSkillData = attackSkillData;
            gameObject.SetActive(true);
            // if (!gameObject.activeInHierarchy)
            // {
            //     return;
            // }

            // _rigidbody.linearVelocity = dir * attackSkillData.ProjectileSpeed;

            CreateBaseSkillEntity(attackSkillData);
        }
        
        private void Update()
        {
            // transform.Translate(Vector2.right * (_attackSkillData.ProjectileSpeed * Time.deltaTime));
            
            gameObject.SetActive(false);
            if (_camera == null || !gameObject.activeInHierarchy)
            {
                return;
            }
            
            var viewportPos = _camera.WorldToViewportPoint(transform.position);
            if (viewportPos.x < 0 || viewportPos.x > 1 || viewportPos.y < 0 || viewportPos.y > 1)
            {
                Release();
            }
        }
    }
}