using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Serializable]
public class UnitSetup
{
    [SerializeField] public float2 Spawn = default;
    [SerializeField, NotNull] public string? Script = default;
}

public class SetupManager : Singleton<SetupManager>
{
    [SerializeField, NotNull] GameObject? Prefab = default;
    [SerializeField, NotNull] UnitSetup[]? Units = default;

    public void Setup()
    {
        World world = ConnectionManager.ServerOrDefaultWorld;

        using EntityQuery unitDatabaseQ = world.EntityManager.CreateEntityQuery(typeof(UnitDatabase));
        if (!unitDatabaseQ.TryGetSingletonEntity<UnitDatabase>(out Entity buildingDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(UnitDatabase)} entity singleton");
            return;
        }

        DynamicBuffer<BufferedUnit> units = world.EntityManager.GetBuffer<BufferedUnit>(buildingDatabase, true);

        BufferedUnit unit = units.FirstOrDefault(v => v.Name == Prefab.name);

        if (unit.Prefab == Entity.Null)
        {
            Debug.LogWarning($"Prefab \"{Prefab.name}\" not found");
            return;
        }

        foreach (UnitSetup unitSetup in Units)
        {
            Entity newUnit = world.EntityManager.Instantiate(unit.Prefab);
            world.EntityManager.SetComponentData(newUnit, LocalTransform.FromPosition(new float3(unitSetup.Spawn.x, 0.5f, unitSetup.Spawn.y)));
            world.EntityManager.SetComponentData(newUnit, new Processor()
            {
                SourceFile = new FileId(unitSetup.Script, NetcodeEndPoint.Server),
            });
        }
    }
}
