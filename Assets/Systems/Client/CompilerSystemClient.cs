using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct CompilerSystemClient : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = default;

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<CompilerStatusRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = new(Allocator.Temp);
            commandBuffer.DestroyEntity(entity);

            // Debug.Log($"Received compilation status for {command.ValueRO.FileName}");

            CompilerManager.Instance.HandleRpc(command.ValueRO);
        }

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<CompilationAnalysticsRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = new(Allocator.Temp);
            commandBuffer.DestroyEntity(entity);

            if (!CompilerManager.Instance.CompiledSources.TryGetValue(command.ValueRO.FileName, out CompiledSource source))
            {
                // Debug.LogWarning($"Received analytics for unknown compiled source \"{command.ValueRO.FileName}\"");
                continue;
            }

            source.Diagnostics.Add(new LanguageCore.Diagnostic(
                command.ValueRO.Level,
                command.ValueRO.Message.ToString(),
                new LanguageCore.Position(command.ValueRO.Position, command.ValueRO.AbsolutePosition),
                command.ValueRO.FileName.ToUri(),
                null
            ));
        }

        if (commandBuffer.IsCreated)
        {
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }
    }
}
