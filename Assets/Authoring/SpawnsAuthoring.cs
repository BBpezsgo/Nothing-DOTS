using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Spawns")]
public class SpawnsAuthoring : MonoBehaviour
{
    [SerializeField] Vector3[] Spawns = System.Array.Empty<Vector3>();

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] GameObject? CoreComputer = default;
    [SerializeField] GameObject? Builder = default;

    [UnityEditor.CustomEditor(typeof(SpawnsAuthoring))]
    class Editor : UnityEditor.Editor
    {
        public void OnSceneGUI()
        {
            SpawnsAuthoring t = (SpawnsAuthoring)target;

            for (int i = 0; i < t.Spawns.Length; i++)
            {
                t.Spawns[i] = UnityEditor.Handles.PositionHandle(
                    new Vector3(t.Spawns[i].x, 0f, t.Spawns[i].z),
                    Quaternion.identity
                );
            }
        }
    }

    void OnDrawGizmos()
    {
        for (int i = 0; i < Spawns.Length; i++)
        {
            Gizmos.color = Color.gray;

            if (CoreComputer != null)
            {
                foreach (MeshFilter item in CoreComputer.GetAllComponents<MeshFilter>())
                {
                    Gizmos.DrawWireMesh(item.sharedMesh, item.transform.position + Spawns[i], item.transform.rotation, item.transform.lossyScale);
                }
            }
            else
            {
                Gizmos.DrawWireCube(
                    new Vector3(Spawns[i].x, 0f, Spawns[i].z),
                    new Vector3(2f, 1f, 2f)
                );
            }

            if (Builder != null)
            {
                foreach (MeshFilter item in Builder.GetAllComponents<MeshFilter>())
                {
                    Gizmos.DrawWireMesh(item.sharedMesh, item.transform.position + Spawns[i] + new Vector3(2f, 0f, 2f), item.transform.rotation, item.transform.lossyScale);
                }
            }
        }
    }
#endif

    class Baker : Baker<SpawnsAuthoring>
    {
        public override void Bake(SpawnsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Spawns>(entity);
            DynamicBuffer<BufferedSpawn> buffer = AddBuffer<BufferedSpawn>(entity);
            foreach (Vector3 spawn in authoring.Spawns)
            {
                buffer.Add(new()
                {
                    Position = spawn,
                    IsOccupied = false,
                });
            }
        }
    }
}
