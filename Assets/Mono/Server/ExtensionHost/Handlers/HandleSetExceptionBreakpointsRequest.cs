using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

partial class DebugHost
{
    protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
    {
        return new SetExceptionBreakpointsResponse()
        {
            Breakpoints = new(),
        };
    }
}
