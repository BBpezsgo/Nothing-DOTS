using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

partial class DebugHost
{
    protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
    {
        Log.Trace("[Handler] Disconnect");

        _isDisconnected = true;

        Continue(null);

        return new DisconnectResponse();
    }
}
