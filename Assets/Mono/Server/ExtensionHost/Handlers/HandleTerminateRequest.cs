using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

partial class DebugHost
{
    protected override TerminateResponse HandleTerminateRequest(TerminateArguments arguments)
    {
        Log.Trace($"[Handler] Terminate");
        throw new ProtocolException($"Terminate isn't supported.");
    }
}
