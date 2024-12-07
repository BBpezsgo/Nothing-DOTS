using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Spawns")]
public class SpawnsAuthoring : MonoBehaviour
{
    [SerializeField] public Vector3[] _spawns = new Vector3[0];

    class Baker : Baker<SpawnsAuthoring>
    {
        public override void Bake(SpawnsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Spawns>(entity);
            DynamicBuffer<BufferedSpawn> buffer = AddBuffer<BufferedSpawn>(entity);
            foreach (Vector3 spawn in authoring._spawns)
            {
                buffer.Add(new BufferedSpawn(spawn, false));
            }
        }
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(SpawnsAuthoring))]
public class SpawnsAuthoringEditor : UnityEditor.Editor
{
    public void OnSceneGUI()
    {
        SpawnsAuthoring t = (SpawnsAuthoring)target;

        for (int i = 0; i < t._spawns.Length; i++)
        {
            t._spawns[i] = UnityEditor.Handles.PositionHandle(
                new Vector3(t._spawns[i].x, 0f, t._spawns[i].z),
                Quaternion.identity
            );
        }
    }
}
#endif
