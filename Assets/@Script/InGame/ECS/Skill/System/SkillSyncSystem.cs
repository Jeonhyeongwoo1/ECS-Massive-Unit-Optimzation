using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

public partial struct SkillSyncSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<SkillSpawnTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var (transform, skillComponent, entity)
                 in SystemAPI.Query<RefRW<LocalTransform>, SkillBridgeComponentData>().WithEntityAccess())
        {
            if (skillComponent.Projectile.gameObject == null || skillComponent.Projectile.gameObject.activeInHierarchy == false)
            {
                ecb.DestroyEntity(entity);
            }
            else
            {
                transform.ValueRW.Position = skillComponent.Projectile.transform.position;
                transform.ValueRW.Rotation = skillComponent.Projectile.transform.rotation;
            }
        }
    }
}