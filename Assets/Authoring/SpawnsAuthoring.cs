using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Spawns")]
public class SpawnsAuthoring : MonoBehaviour
{
    [SerializeField] Vector3[] Spawns = new Vector3[0];

#if UNITY_EDITOR
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
#endif

    void OnDrawGizmos()
    {
        for (int i = 0; i < Spawns.Length; i++)
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(
                new Vector3(Spawns[i].x, 0f, Spawns[i].z),
                new Vector3(2f, 1f, 2f)
            );
        }
    }

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
