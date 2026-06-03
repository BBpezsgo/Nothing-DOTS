using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

partial class DebugHost
{
    protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
    {
        SetBreakpointsResponse res = base.HandleSetBreakpointsRequest(arguments);
        RefreshProcessorBreakpoints();
        return res;
    }
}
