using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MewVivor.Common;
using MewVivor.Data;
using MewVivor.Enum;
using MewVivor.InGame.Controller;
using MewVivor.Key;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MewVivor.InGame.Skill
{
    /// <summary>
    ///  스킬 설명 :  캐릭터 후방에 부채꼴 범위로 공격함
    ///             꼬리에 닿은 적들은 넉백
    /// </summary>
    public class Skill_10061 : RepeatAttackSkill
    {
        private IGeneratable _generatable;
        
        
        public override void Initialize(CreatureController owner, AttackSkillData attackSkillData)
        {
            base.Initialize(owner, attackSkillData);
            GameObject prefab = Manager.I.Resource.Instantiate(AttackSkillData.PrefabLabel, false);
            _generatable = prefab.GetComponent<IGeneratable>();
            _generatable.OnHit = OnHit;
            _generatable.ProjectileMono.transform.SetParent(owner.transform);
            _generatable.ProjectileMono.gameObject.SetActive(false);
        }
        
        public override void StopSkillLogic()
        {
            Utils.SafeCancelCancellationTokenSource(ref _skillLogicCts);
            Release();
        }

        protected override async UniTask UseSkill()
        {
            Manager.I.Audio.Play(Sound.SFX, SoundKey.UseSkill_10061).Forget();
            var player = _owner as PlayerController;
            _generatable.Generate(player.AttackPoint, _owner.GetDirection(), AttackSkillData, _owner, CurrentLevel);

            // List<Transform> list = null;
            // if (IsMaxLevel)
            // {
            //     list = Manager.I.Object
            //         .GetMonsterAndBossTransformListInFanShape(_owner.transform,
            //             _owner.GetDirection() * -1,
            //             AttackSkillData.AttackRange,
            //             360);
            // }
            // else
            // {
            //     list = Manager.I.Object
            //         .GetMonsterAndBossTransformListInFanShape(_owner.transform,
            //             _owner.GetDirection() * -1,
            //             AttackSkillData.AttackRange,
            //             AttackSkillData.ConeAngle);
            // }
            //
            // if (list != null)
            // {
            //     foreach (Transform tr in list)
            //     {
            //         OnHit(tr, _generatable.ProjectileMono);
            //     }
            // }
            
            await UniTask.WaitForSeconds(0.3f);
            _generatable.Release();
        }
    }
}