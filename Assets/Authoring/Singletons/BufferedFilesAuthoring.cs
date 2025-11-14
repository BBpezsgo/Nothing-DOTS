using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Buffered Files")]
class BufferedFilesAuthoring : MonoBehaviour
{
    class Baker : Baker<BufferedFilesAuthoring>
    {
        public override void Bake(BufferedFilesAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<BufferedFiles>(entity, new());
            AddBuffer<BufferedReceivingFileChunk>(entity);
            AddBuffer<BufferedReceivingFile>(entity);
            AddBuffer<BufferedSendingFile>(entity);
            AddBuffer<BufferedSentFileChunk>(entity);
        }
    }
}
