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
    [SerializeField, NotNull] public GameObject? Prefab = default;
    [SerializeField, NotNull] public UnitSetup[]? Units = default;

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

        BufferedUnit unit = units.FirstOrDefault(static (v, c) => v.Name == c, Prefab.name);

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

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(SetupManager))]
public class SetupManagerEditor : UnityEditor.Editor
{
    public void OnSceneGUI()
    {
        SetupManager t = (SetupManager)target;

        for (int i = 0; i < t.Units.Length; i++)
        {
            Vector3 p = UnityEditor.Handles.PositionHandle(
                new Vector3(t.Units[i].Spawn.x, 0f, t.Units[i].Spawn.y),
                Quaternion.identity
            );
            t.Units[i].Spawn = new float2(p.x, p.z);
        }
    }
}
#endif
