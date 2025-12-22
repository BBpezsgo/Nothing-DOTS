using Segments;
using Unity.Entities;

partial struct SegmentsInitializer : ISystem
{
    void ISystem.OnCreate(ref SystemState state)
    {
        Core.Initialize();
    }
}
