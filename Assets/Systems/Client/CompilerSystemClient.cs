using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
partial struct CompilerSystemClient : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = default;

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CompilerStatusRpc>>()
            .WithEntityAccess())
        {
            // Debug.Log($"Received compilation status for {command.ValueRO.FileName}");

            CompilerManager.Instance.HandleRpc(command.ValueRO);

            if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
            entityCommandBuffer.DestroyEntity(entity);
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CompilationAnalysticsRpc>>()
            .WithEntityAccess())
        {
            if (!CompilerManager.Instance.CompiledSources.TryGetValue(command.ValueRO.FileName, out CompiledSource source))
            {
                // Debug.LogWarning($"Received analytics for unknown compiled source \"{command.ValueRO.FileName}\"");
                if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
                entityCommandBuffer.DestroyEntity(entity);
                continue;
            }

            source.Diagnostics.Add(new LanguageCore.Diagnostic(
                command.ValueRO.Level,
                command.ValueRO.Message.ToString(),
                new LanguageCore.Position(command.ValueRO.Position, default),
                command.ValueRO.FileName.ToUri()
            ));

            if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
            entityCommandBuffer.DestroyEntity(entity);
        }

        if (entityCommandBuffer.IsCreated)
        {
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }
    }
}
