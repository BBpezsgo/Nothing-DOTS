using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

partial class DebugHost
{
    protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
    {
        InitializeResponse res = base.HandleInitializeRequest(arguments);
        res.SupportsConfigurationDoneRequest = false;
        return res;
    }
}
