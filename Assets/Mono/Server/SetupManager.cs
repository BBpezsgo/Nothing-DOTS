using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Serializable]
public class UnitSetup
{
    [SerializeField, NotNull] public GameObject? Prefab = default;
    [SerializeField] public float2 Spawn = default;
    [SerializeField, NotNull] public string? Script = default;
    [SerializeField] public int Team = default;
}

public class SetupManager : Singleton<SetupManager>
{
    [Header("Exact Spawns")]
    [SerializeField] bool SpawnExactUnits = default;
    [SerializeField, NotNull] public UnitSetup[]? Units = default;

    [Header("Generator")]
    [SerializeField] public int Team = default;
    [SerializeField, NotNull] public string? GeneratedScript = default;
    [SerializeField] public int GeneratedCount = default;
    [SerializeField] public float Density = 0f;
    [SerializeField] public Vector2 Start = default;
    [SerializeField] public Vector2 End = default;
    [SerializeField] public GameObject? RandomUnitPrefab = default;

    [SerializeField] public bool RandomRotation = default;
    [SerializeField] public bool RandomPosition = default;

    [SerializeField] bool Deterministic = default;
    [ShowIf(nameof(Deterministic)), SerializeField] int RandomSeed = default;

    const float UnitRadius = 1.45f;
    const float UnitArea = UnitRadius * UnitRadius * MathF.PI;
    const float PositionY = 0f;

    void OnValidate()
    {
        if (Start.x > End.x) Start.x = End.x;
        if (Start.y > End.y) Start.y = End.y;
    }

    public void Setup()
    {
        World world = ConnectionManager.ServerOrDefaultWorld;

        DynamicBuffer<BufferedUnit> units;
        DynamicBuffer<BufferedBuilding> buildings;

        using (EntityQuery unitDatabaseQ = world.EntityManager.CreateEntityQuery(typeof(UnitDatabase)))
        {
            if (!unitDatabaseQ.TryGetSingletonEntity<UnitDatabase>(out Entity unitDatabase))
            {
                Debug.LogWarning($"Failed to get {nameof(UnitDatabase)} entity singleton");
                return;
            }
            units = world.EntityManager.GetBuffer<BufferedUnit>(unitDatabase, true);
        }

        using (EntityQuery buildingDatabaseQ = world.EntityManager.CreateEntityQuery(typeof(BuildingDatabase)))
        {
            if (!buildingDatabaseQ.TryGetSingletonEntity<BuildingDatabase>(out Entity buildingDatabase))
            {
                Debug.LogWarning($"Failed to get {nameof(BuildingDatabase)} entity singleton");
                return;
            }
            buildings = world.EntityManager.GetBuffer<BufferedBuilding>(buildingDatabase, true);
        }

        if (SpawnExactUnits)
        {
            foreach (UnitSetup unitSetup in Units)
            {
                Entity prefab;

                BufferedUnit unit = units.FirstOrDefault(static (v, c) => v.Name == c, unitSetup.Prefab.name);
                BufferedBuilding building = buildings.FirstOrDefault(static (v, c) => v.Name == c, unitSetup.Prefab.name);

                if (unit.Prefab != Entity.Null)
                {
                    prefab = unit.Prefab;
                }
                else if (building.Prefab != Entity.Null)
                {
                    prefab = building.Prefab;
                }
                else
                {
                    Debug.LogWarning($"Prefab \"{unitSetup.Prefab.name}\" not found");
                    continue;
                }

                Entity newUnit = world.EntityManager.Instantiate(prefab);
                world.EntityManager.SetComponentData(newUnit, LocalTransform.FromPosition(new float3(unitSetup.Spawn.x, PositionY, unitSetup.Spawn.y)));
                world.EntityManager.SetComponentData(newUnit, new Processor()
                {
                    SourceFile = new FileId(unitSetup.Script, NetcodeEndPoint.Server),
                });
                world.EntityManager.SetComponentData(newUnit, new UnitTeam()
                {
                    Team = Team,
                });
            }
        }

        StartCoroutine(SpawnRandomUnits(units, world));
    }

    IEnumerator SpawnRandomUnits(DynamicBuffer<BufferedUnit> units, World world)
    {
        System.Random random = Deterministic ? new System.Random(RandomSeed) : RandomManaged.Shared;

        if (RandomUnitPrefab == null)
        { yield break; }

        Vector2 start = Start;
        Vector2 end = End;

        if (Density != 0f)
        {
            float width = MathF.Sqrt(UnitArea * Density * GeneratedCount);
            start = new Vector2(width, width) * -0.5f;
            end = new Vector2(width, width) * 0.5f;
        }

        BufferedUnit prefab = units.FirstOrDefault(static (v, c) => v.Name == c, RandomUnitPrefab.name);

        if (prefab.Prefab == Entity.Null)
        {
            Debug.LogWarning($"Prefab \"{RandomUnitPrefab.name}\" not found");
            yield break;
        }

        yield return new WaitForSecondsRealtime(0.1f);

        int c = 0;

        foreach (Vector2 generated in GetPositions())
        {
            if (c++ > 50)
            {
                yield return null;
                c = 0;
            }


            Entity newUnit = world.EntityManager.Instantiate(prefab.Prefab);
            if (RandomRotation)
            {
                world.EntityManager.SetComponentData(newUnit, LocalTransform.FromPositionRotation(
                    new float3(generated.x, PositionY, generated.y),
                    quaternion.EulerXYZ(0f, random.Float(0f, math.TAU), 0f)
                ));
            }
            else
            {
                world.EntityManager.SetComponentData(newUnit, LocalTransform.FromPosition(
                    new float3(generated.x, PositionY, generated.y)
                ));
            }
            world.EntityManager.SetComponentData(newUnit, new Processor()
            {
                SourceFile = new FileId(GeneratedScript, NetcodeEndPoint.Server),
            });
            world.EntityManager.SetComponentData(newUnit, new UnitTeam()
            {
                Team = Team,
            });
        }
    }

    IEnumerable<Vector2> GetPositions()
    {
        Vector2 start = Start;
        Vector2 end = End;

        if (Density != 0f)
        {
            float width = MathF.Sqrt(UnitArea * Density * GeneratedCount);
            start = new Vector2(width, width) * -0.5f;
            end = new Vector2(width, width) * 0.5f;
        }

        System.Random random = Deterministic ? new System.Random(RandomSeed) : RandomManaged.Shared;

        List<float2> spawned = new(GeneratedCount);

        bool IsOccupied(float2 position)
        {
            if (SpawnExactUnits)
            {
                foreach (UnitSetup unit in Units)
                {
                    if (math.distance(unit.Spawn, position) < 2f * UnitRadius) return true;
                }
            }

            foreach (float2 unit in spawned)
            {
                if (math.distance(unit, position) < 2f * UnitRadius) return true;
            }

            return false;
        }

        if (RandomPosition)
        {
            for (int i = 0; i < GeneratedCount; i++)
            {
                for (int j = 0; j < 50; j++)
                {
                    float2 generated = new(
                        random.Float(start.x, end.x),
                        random.Float(start.y, end.y)
                    );

                    if (IsOccupied(generated)) continue;

                    spawned.Add(generated);
                    yield return generated;

                    goto ok;
                }

                //Debug.LogWarning($"Only spawned {i} but had to {GeneratedCount}");
                yield break;

            ok:;
            }

            //Debug.Log($"Spawned {GeneratedCount}");
        }
        else if (GeneratedCount > 0)
        {
            float width = end.x - start.x;
            float height = end.y - start.y;

            float columns = math.sqrt(GeneratedCount * width / height);
            float rows = GeneratedCount / columns;

            float dx = math.max(0.1f, width / math.max(1f, columns - 1));
            float dy = math.max(0.1f, height / math.max(1f, rows - 1));
            int i = 0;

            for (float x = start.x; x <= end.x; x += dx)
            {
                for (float y = start.y; y <= end.y; y += dy)
                {
                    float2 generated = new(x, y);

                    if (IsOccupied(generated)) continue;

                    spawned.Add(generated);
                    yield return generated;

                    if (++i >= GeneratedCount) break;
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!RandomPosition || Deterministic)
        {
            foreach (Vector2 position in GetPositions())
            {
                Gizmos.DrawSphere(new Vector3(position.x, 0.5f, position.y), UnitRadius);
            }
        }

        if (SpawnExactUnits)
        {
            for (int i = 0; i < Units.Length; i++)
            {
                Gizmos.DrawSphere(new Vector3(Units[i].Spawn.x, 0.5f, Units[i].Spawn.y), 1f);
            }
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

        Vector3 start = UnityEditor.Handles.PositionHandle(
            new Vector3(t.Start.x, 0f, t.Start.y),
            quaternion.identity
        );
        Vector3 end = UnityEditor.Handles.PositionHandle(
            new Vector3(t.End.x, 0f, t.End.y),
            quaternion.identity
        );
        t.Start = new Vector2(start.x, start.z);
        t.End = new Vector2(end.x, end.z);
    }
}
#endif
