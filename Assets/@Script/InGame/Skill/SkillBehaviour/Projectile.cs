using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MewVivor.Data;
using MewVivor.InGame.Enum;
using Unity.Entities;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MewVivor.InGame.Skill
{
    public abstract class Projectile : MonoBehaviour, IGeneratable
    {
        public Vector3 Velocity => _rigidbody.linearVelocity;
        public Action<Transform, Projectile> OnHit { get; set; }
        public Action<Transform, Projectile> OnExit { get; set; }
        public Projectile ProjectileMono => this;

        public bool IsRelease { get; private set; }
        public int Level { get; set; }

        [SerializeField] protected Rigidbody2D _rigidbody;

        protected bool wantToSleepInTriggerEnter;

        public virtual void Generate(Transform targetTransform, Vector3 direction, AttackSkillData attackSkillData, CreatureController owner, int currentLevel) {}
        public virtual void Generate(Vector3 spawnPosition, Vector3 direction, AttackSkillData attackSkillData, CreatureController owner) {}

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(Tag.Monster) || other.CompareTag(Tag.ItemBox))
            {
                OnHit?.Invoke(other.transform, this);
                if (wantToSleepInTriggerEnter)
                {
                    Release();
                }
            }
        }
        
        protected virtual void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag(Tag.Monster) || other.CompareTag(Tag.ItemBox))
            {
                OnExit?.Invoke(other.transform, this);
            }
        }

        private void OnEnable()
        {
            IsRelease = false;
        }

        protected virtual void OnDestroy()
        {
            CancelInvoke();
        }

        public virtual void Release()
        {
            if (IsRelease || gameObject.IsDestroyed())
            {
                return;
            }

            IsRelease = true;
            Manager.I.Pool.ReleaseObject(gameObject.name, gameObject);
        }

        public virtual void OnChangedSkillData(AttackSkillData attackSkillData)
        {
            
        }

        public virtual void OnHitMonsterEntity(Unity.Entities.Entity monsterEntity)
        {
            
        }

        protected async UniTaskVoid ApplyDamagedAsync(CancellationTokenSource cancellationTokenSource, Action damageAction)
        {
            CancellationToken token = cancellationTokenSource.Token;
            float interval = 0.1f;
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                damageAction?.Invoke();
                
                try
                {
                    await UniTask.WaitForSeconds(interval, cancellationToken: token);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    Debug.LogError($"{nameof(ApplyDamagedAsync)} error {e.Message}");
                    break;
                }
            }
            
            Release();
        }
        
        protected IEnumerator LaunchParabolaProjectile(Vector3 startPosition, Vector3 targetPosition, float projectileSpeed, float heightArc, Action callback = null)
        {
            float journeyLength = Vector2.Distance(startPosition, targetPosition);
            float totalTime = journeyLength / projectileSpeed;
            float elapsedTime = 0;

            transform.DORotate(new Vector3(0, 0, Random.Range(180, 360)), totalTime);
            while (elapsedTime < totalTime)
            {
                elapsedTime += Time.deltaTime;

                float normalizedTime = elapsedTime / totalTime;

                // 포물선 모양으로 이동
                float x = Mathf.Lerp(startPosition.x, targetPosition.x, normalizedTime);
                float baseY = Mathf.Lerp(startPosition.y, targetPosition.y, normalizedTime);
                float arc = heightArc * Mathf.Sin(normalizedTime * Mathf.PI);
                float y = baseY + arc;
                
                var nextPos = new Vector3(x, y);
                transform.position = nextPos;
                yield return null;
            }
            
            callback?.Invoke();
        }
        
        protected IEnumerator WaitDuration(float duration, Action done)
        {
            yield return new WaitForSeconds(duration);
            done?.Invoke();
        }

        protected virtual Unity.Entities.Entity CreateBaseSkillEntity(AttackSkillData attackSkillData, bool isIntervalAttack = false,
            float intervalAttackTime = 0, int attackCount = 1)
        {
            var skillSpawnSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SkillSpawnSystem>();
            return skillSpawnSystem.CreateBaseSkillEntity(this, attackSkillData, isIntervalAttack, intervalAttackTime,
                attackCount);
        }

        protected virtual Unity.Entities.Entity CreateExplosionSkillComponent(Unity.Entities.Entity skillEntity, float attackRange)
        {
            var skillSpawnSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SkillSpawnSystem>();
            return skillSpawnSystem.CreateExplosionSkill(skillEntity, attackRange);
        }
    }
}