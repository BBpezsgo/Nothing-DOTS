using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

partial class DebugHost
{
    protected override SetInstructionBreakpointsResponse HandleSetInstructionBreakpointsRequest(SetInstructionBreakpointsArguments arguments)
    {
        SetInstructionBreakpointsResponse res = base.HandleSetInstructionBreakpointsRequest(arguments);
        RefreshProcessorBreakpoints();
        return res;
    }
}
