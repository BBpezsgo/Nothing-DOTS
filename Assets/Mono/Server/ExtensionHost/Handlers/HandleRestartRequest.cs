using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Unity.Entities;

partial class DebugHost
{
    protected override RestartResponse HandleRestartRequest(RestartArguments arguments)
    {
        Log.Trace($"[Handler] Restart");
        EntityManager e = ConnectionManager.ServerOrDefaultWorld.EntityManager;
        if (e.Exists(_entity))
        {
            Processor processor = e.GetComponentData<Processor>(_entity);
            ProcessorSourceSystemServer.ResetProcessor(ref processor);
            processor.DebugContext.Stopped = ProcessorJob.StopReason.No;
            processor.DebugContext.IsStopUnhandled = true;
            e.SetComponentData(_entity, processor);
            return new RestartResponse();
        }
        else
        {
            throw new ProtocolException($"Entity {_entity} got destroyed");
        }
    }
}
