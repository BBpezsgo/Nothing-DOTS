using Unity.Entities;
using UnityEngine;

#nullable enable

[AddComponentMenu("Authoring/BufferedFiles")]
public class BufferedFilesAuthoring : MonoBehaviour
{
    class Baker : Baker<BufferedFilesAuthoring>
    {
        public override void Bake(BufferedFilesAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new BufferedFiles());
            AddBuffer<BufferedFileChunk>(entity);
            AddBuffer<BufferedReceivingFile>(entity);
            AddBuffer<BufferedSendingFile>(entity);
        }
    }
}
