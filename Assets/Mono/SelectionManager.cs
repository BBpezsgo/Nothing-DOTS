using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class SelectionManager : Singleton<SelectionManager>
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Entity hit = RayCast(Camera.main.ScreenPointToRay(Input.mousePosition));
            if (hit == Entity.Null) return;
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!entityManager.HasComponent<SelectableUnit>(hit))
            {
                Debug.LogError($"Entity {hit} doesn't have a {nameof(SelectableUnit)} component");
                return;
            }

            if (entityManager.HasComponent<Factory>(hit))
            {
                FactoryManager.Instance.OpenUI(hit);
            }
        }
    }

    Entity RayCast(UnityEngine.Ray ray)
    {
        EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsWorldSingleton>();

        EntityQuery singletonQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(builder);
        CollisionWorld collisionWorld = singletonQuery.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        singletonQuery.Dispose();

        RaycastInput input = new()
        {
            Start = ray.origin,
            End = ray.GetPoint(100f),
            Filter = new CollisionFilter()
            {
                BelongsTo = ~0u,
                CollidesWith = 1u << 6,
                GroupIndex = 0,
            },
        };

        if (collisionWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
        { return hit.Entity; }

        return Entity.Null;
    }
}
