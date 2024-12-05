using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/BufferedFiles")]
public class BufferedFilesAuthoring : MonoBehaviour
{
    class Baker : Baker<BufferedFilesAuthoring>
    {
        public override void Bake(BufferedFilesAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new BufferedFiles());
            AddBuffer<BufferedReceivingFileChunk>(entity);
            AddBuffer<BufferedReceivingFile>(entity);
            AddBuffer<BufferedSendingFile>(entity);
            AddBuffer<BufferedSentFileChunk>(entity);
        }
    }
}
