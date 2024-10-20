using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

#nullable enable

public class SelectionManager : Singleton<SelectionManager>
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !UI.IsMouseCaptured)
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
                return;
            }

            if (entityManager.HasComponent<Unit>(hit))
            {
                TerminalManager.Instance.OpenUI(hit);
                return;
            }
        }
    }

    Entity RayCast(UnityEngine.Ray ray)
    {
        EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsWorldSingleton>();

        CollisionWorld collisionWorld;
        using (EntityQuery singletonQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(builder))
        {
            collisionWorld = singletonQuery.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        }

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
