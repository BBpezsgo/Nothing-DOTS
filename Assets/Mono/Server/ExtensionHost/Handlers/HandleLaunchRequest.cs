using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

partial class DebugHost
{
    protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
    {
        Log.Trace($"[Handler] Launch");
        throw new ProtocolException($"Launch isn't supported.");
    }
}
