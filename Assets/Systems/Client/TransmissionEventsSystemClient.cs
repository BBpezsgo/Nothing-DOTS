using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
partial class TransmissionEventsSystemClient : SystemBase
{
    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = default;

        foreach (var (_, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<WirelessTransmissionEventRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            var p1 = GUIUtility.ScreenToGUIPoint(MainCamera.Camera.WorldToScreenPoint(command.ValueRO.Origin));
            var p2 = GUIUtility.ScreenToGUIPoint(MainCamera.Camera.WorldToScreenPoint(command.ValueRO.Destination));
        }
    }
}
