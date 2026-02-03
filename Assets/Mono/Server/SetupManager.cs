using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class SetupManager : Singleton<SetupManager>
{
    [Serializable]
    class UnitSetup
    {
        [SerializeField, NotNull] public GameObject? Prefab = default;
        [SerializeField] public float2 Spawn = default;
        [SerializeField, NotNull] public string? Script = default;
        [SerializeField] public int Team = default;
    }

    [Header("Exact Spawns")]
    [SerializeField] bool SpawnExactUnits = default;
    [SerializeField, NotNull] UnitSetup[]? Units = default;

    [Header("Generator")]
    [SerializeField] int Team = default;
    [SerializeField, NotNull] string? GeneratedScript = default;
    [SerializeField, Min(0)] int GeneratedCount = default;
    [SerializeField, Min(0)] float Density = 0f;
    [SerializeField] Vector2 Start = default;
    [SerializeField] Vector2 End = default;
    [SerializeField] GameObject? RandomUnitPrefab = default;

    [SerializeField] bool RandomRotation = default;
    [SerializeField] bool RandomPosition = default;

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
        TerrainFeaturePrefabs terrainPrefabs;

        using (EntityQuery unitDatabaseQ = world.EntityManager.CreateEntityQuery(typeof(UnitDatabase)))
        {
            if (!unitDatabaseQ.TryGetSingletonEntity<UnitDatabase>(out Entity unitDatabase))
            {
                Debug.LogError($"{DebugEx.ServerPrefix} Failed to get {nameof(UnitDatabase)} entity singleton");
                return;
            }
            units = world.EntityManager.GetBuffer<BufferedUnit>(unitDatabase, true);
        }

        using (EntityQuery buildingDatabaseQ = world.EntityManager.CreateEntityQuery(typeof(BuildingDatabase)))
        {
            if (!buildingDatabaseQ.TryGetSingletonEntity<BuildingDatabase>(out Entity buildingDatabase))
            {
                Debug.LogError($"{DebugEx.ServerPrefix} Failed to get {nameof(BuildingDatabase)} entity singleton");
                return;
            }
            buildings = world.EntityManager.GetBuffer<BufferedBuilding>(buildingDatabase, true);
        }

        using (EntityQuery terrainFeaturePrefabsQ = world.EntityManager.CreateEntityQuery(typeof(TerrainFeaturePrefabs)))
        {
            if (!terrainFeaturePrefabsQ.TryGetSingleton(out terrainPrefabs))
            {
                Debug.LogError($"{DebugEx.ServerPrefix} Failed to get {nameof(TerrainFeaturePrefabs)} singleton");
                return;
            }
        }

        if (SpawnExactUnits)
        {
            foreach (UnitSetup unitSetup in Units)
            {
                Entity prefab = Entity.Null;

                if (prefab == Entity.Null)
                {
                    BufferedUnit unit = units.FirstOrDefault(static (v, c) => v.Name == c, unitSetup.Prefab.name);
                    prefab = unit.Prefab;
                }

                if (prefab == Entity.Null)
                {
                    BufferedBuilding building = buildings.FirstOrDefault(static (v, c) => v.Name == c, unitSetup.Prefab.name);
                    prefab = building.Prefab;
                }

                if (prefab == Entity.Null)
                {
                    if (unitSetup.Prefab.name == terrainPrefabs.ObstaclePrefabName) prefab = terrainPrefabs.ObstaclePrefab;
                    else if (unitSetup.Prefab.name == terrainPrefabs.ResourcePrefabName) prefab = terrainPrefabs.ResourcePrefab;
                }

                if (prefab == Entity.Null)
                {
                    Debug.LogError($"{DebugEx.ServerPrefix} Prefab \"{unitSetup.Prefab.name}\" not found");
                    continue;
                }

                Entity newUnit = world.EntityManager.Instantiate(prefab);
                world.EntityManager.SetComponentData(newUnit, LocalTransform.FromPosition(new float3(unitSetup.Spawn.x, PositionY, unitSetup.Spawn.y)));
                if (world.EntityManager.HasComponent<Processor>(newUnit))
                {
                    world.EntityManager.ModifyComponent(newUnit, (ref Processor v) =>
                    {
                        v.SourceFile = new FileId(unitSetup.Script, NetcodeEndPoint.Server);
                    });
                }
                if (world.EntityManager.HasComponent<UnitTeam>(newUnit))
                {
                    world.EntityManager.ModifyComponent(newUnit, (ref UnitTeam v) =>
                    {
                        v.Team = Team;
                    });
                }
            }
        }

        if (RandomUnitPrefab != null)
        {
            Entity prefab = Entity.Null;

            if (prefab == Entity.Null)
            {
                prefab = units.FirstOrDefault(static (v, c) => v.Name == c, RandomUnitPrefab.name).Prefab;
            }

            if (prefab == Entity.Null)
            {
                Debug.LogError($"{DebugEx.ServerPrefix} Prefab \"{RandomUnitPrefab.name}\" not found");
            }
            else
            {
                StartCoroutine(SpawnRandomUnits(prefab, world));
            }
        }
    }

    IEnumerator SpawnRandomUnits(Entity prefab, World world)
    {
        if (RandomUnitPrefab == null)
        { yield break; }

        yield return new WaitForSecondsRealtime(0.1f);

        int c = 0;

        foreach ((Vector2 position, float rotation) in GetPositions())
        {
            if (c++ > 50)
            {
                yield return null;
                c = 0;
            }

            Entity newUnit = world.EntityManager.Instantiate(prefab);
            if (RandomRotation)
            {
                world.EntityManager.SetComponentData(newUnit, LocalTransform.FromPositionRotation(
                    new float3(position.x, PositionY, position.y),
                    quaternion.EulerXYZ(0f, rotation, 0f)
                ));
            }
            else
            {
                world.EntityManager.SetComponentData(newUnit, LocalTransform.FromPosition(
                    new float3(position.x, PositionY, position.y)
                ));
            }
            if (world.EntityManager.HasComponent<Processor>(newUnit))
            {
                world.EntityManager.SetComponentData(newUnit, new Processor()
                {
                    SourceFile = new FileId(GeneratedScript, NetcodeEndPoint.Server),
                });
            }
            if (world.EntityManager.HasComponent<UnitTeam>(newUnit))
            {
                world.EntityManager.SetComponentData(newUnit, new UnitTeam()
                {
                    Team = Team,
                });
            }
        }
    }

    IEnumerable<(Vector2 Position, float Rotation)> GetPositions()
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
                    yield return (generated, RandomRotation ? random.Float(0f, math.TAU) : 0f);

                    goto ok;
                }

                //Debug.LogWarning($"Only spawned `{i}` but had to {GeneratedCount}");
                yield break;

            ok:;
            }

            //Debug.Log($"{DebugEx.ServerPrefix} Spawned {GeneratedCount}");
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
                    yield return (generated, RandomRotation ? random.Float(0f, math.TAU) : 0f);

                    if (++i >= GeneratedCount) break;
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.gray;

        if (!RandomPosition || Deterministic)
        {
            int i = 0;
            List<MeshFilter> meshes = RandomUnitPrefab == null ? new() : RandomUnitPrefab.GetAllComponents<MeshFilter>();
            foreach ((Vector2 position, float rotation) in GetPositions())
            {
                if (i++ > 100) break;
                bool v = false;
                Vector3 p = new(position.x, PositionY, position.y);

                foreach (MeshFilter item in meshes)
                {
                    Gizmos.DrawWireMesh(item.sharedMesh, item.transform.position + p, item.transform.rotation, item.transform.lossyScale);
                    v = true;
                }

                if (v) continue;

                Gizmos.DrawSphere(p, UnitRadius);
            }
        }

        if (SpawnExactUnits)
        {
            for (int i = 0; i < Units.Length; i++)
            {
                bool v = false;
                Vector3 p = new(Units[i].Spawn.x, PositionY, Units[i].Spawn.y);

                foreach (MeshFilter item in Units[i].Prefab.GetAllComponents<MeshFilter>())
                {
                    Gizmos.DrawWireMesh(item.sharedMesh, item.transform.position + p, item.transform.rotation, item.transform.lossyScale);
                    v = true;
                }

                if (v) continue;

                Gizmos.DrawSphere(p, 1f);
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
                    new Vector3(t.Units[i].Spawn.x, PositionY, t.Units[i].Spawn.y),
                    Quaternion.identity
                );
                t.Units[i].Spawn = new float2(p.x, p.z);
            }

            Vector3 start = UnityEditor.Handles.PositionHandle(
                new Vector3(t.Start.x, PositionY, t.Start.y),
                quaternion.identity
            );
            Vector3 end = UnityEditor.Handles.PositionHandle(
                new Vector3(t.End.x, PositionY, t.End.y),
                quaternion.identity
            );
            t.Start = new Vector2(start.x, start.z);
            t.End = new Vector2(end.x, end.z);
        }
    }
#endif
}
