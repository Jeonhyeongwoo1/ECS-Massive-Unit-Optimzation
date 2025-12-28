using MewVivor;
using MewVivor.InGame.Controller;
using Unity.Collections;
using Unity.Entities;

public partial class MonsterAttackSystemBase : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (monsterAttackEventComponent, entity) 
                 in SystemAPI.Query<RefRO<MonsterAttackEventComponent>>().WithEntityAccess())
        {
            var monsterEntity = monsterAttackEventComponent.ValueRO.MonsterEntity;
            if (monsterEntity == Entity.Null || !SystemAPI.Exists(monsterEntity))
            {
                continue;
            }
            
            var monsterComponent = SystemAPI.GetComponentRO<MonsterComponent>(monsterEntity);
            float atk = monsterComponent.ValueRO.Atk;
            PlayerController player = Manager.I.Object.Player;

            if (!player.IsDead)
            {
                player.TakeDamage(atk);
            }

            ecb.DestroyEntity(entity);
        }
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}