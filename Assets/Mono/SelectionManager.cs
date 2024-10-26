using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

public class SelectionManager : Singleton<SelectionManager>
{
    Entity _firstHit = Entity.Null;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !UI.IsMouseCaptured)
        {
            _firstHit = Entity.Null;

            Entity hit = RayCast(Camera.main.ScreenPointToRay(Input.mousePosition));
            if (hit == Entity.Null) return;
            EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

            if (!entityManager.HasComponent<SelectableUnit>(hit))
            {
                Debug.LogError($"Entity {hit} doesn't have a {nameof(SelectableUnit)} component");
                return;
            }

            _firstHit = hit;
        }

        if (Input.GetMouseButtonUp(0) && !UI.IsMouseCaptured && _firstHit != Entity.Null)
        {
            Entity hit = RayCast(Camera.main.ScreenPointToRay(Input.mousePosition));
            if (hit == Entity.Null)
            {
                _firstHit = Entity.Null;
                return;
            }
            EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

            if (hit == _firstHit)
            {
                _firstHit = Entity.Null;

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
            _firstHit = Entity.Null;
        }
    }

    Entity RayCast(UnityEngine.Ray ray)
    {
        EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsWorldSingleton>();

        CollisionWorld collisionWorld;
        using (EntityQuery singletonQuery = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(builder))
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
