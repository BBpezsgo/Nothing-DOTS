
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

partial class DebugHost
{
    protected override void HandleGotoRequestAsync(IRequestResponder<GotoArguments> responder) => _manager.ScheduleRequest(HandleGotoRequest, responder);
    protected override void HandleNextRequestAsync(IRequestResponder<NextArguments> responder) => _manager.ScheduleRequest(HandleNextRequest, responder);
    protected override void HandlePauseRequestAsync(IRequestResponder<PauseArguments> responder) => _manager.ScheduleRequest(HandlePauseRequest, responder);
    protected override void HandleAttachRequestAsync(IRequestResponder<AttachArguments> responder) => _manager.ScheduleRequest(HandleAttachRequest, responder);
    protected override void HandleCancelRequestAsync(IRequestResponder<CancelArguments> responder) => _manager.ScheduleRequest(HandleCancelRequest, responder);
    protected override void HandleLaunchRequestAsync(IRequestResponder<LaunchArguments> responder) => _manager.ScheduleRequest(HandleLaunchRequest, responder);
    protected override void HandleScopesRequestAsync(IRequestResponder<ScopesArguments, ScopesResponse> responder) => _manager.ScheduleRequest(HandleScopesRequest, responder);
    protected override void HandleSourceRequestAsync(IRequestResponder<SourceArguments, SourceResponse> responder) => _manager.ScheduleRequest(HandleSourceRequest, responder);
    protected override void HandleStepInRequestAsync(IRequestResponder<StepInArguments> responder) => _manager.ScheduleRequest(HandleStepInRequest, responder);
    protected override void HandleModulesRequestAsync(IRequestResponder<ModulesArguments, ModulesResponse> responder) => _manager.ScheduleRequest(HandleModulesRequest, responder);
    protected override void HandleRestartRequestAsync(IRequestResponder<RestartArguments> responder) => _manager.ScheduleRequest(HandleRestartRequest, responder);
    protected override void HandleStepOutRequestAsync(IRequestResponder<StepOutArguments> responder) => _manager.ScheduleRequest(HandleStepOutRequest, responder);
    protected override void HandleThreadsRequestAsync(IRequestResponder<ThreadsArguments, ThreadsResponse> responder) => _manager.ScheduleRequest(HandleThreadsRequest, responder);
    protected override void HandleContinueRequestAsync(IRequestResponder<ContinueArguments, ContinueResponse> responder) => _manager.ScheduleRequest(HandleContinueRequest, responder);
    protected override void HandleEvaluateRequestAsync(IRequestResponder<EvaluateArguments, EvaluateResponse> responder) => _manager.ScheduleRequest(HandleEvaluateRequest, responder);
    protected override void HandleStepBackRequestAsync(IRequestResponder<StepBackArguments> responder) => _manager.ScheduleRequest(HandleStepBackRequest, responder);
    protected override void HandleLocationsRequestAsync(IRequestResponder<LocationsArguments, LocationsResponse> responder) => _manager.ScheduleRequest(HandleLocationsRequest, responder);
    protected override void HandleTerminateRequestAsync(IRequestResponder<TerminateArguments> responder) => _manager.ScheduleRequest(HandleTerminateRequest, responder);
    protected override void HandleVariablesRequestAsync(IRequestResponder<VariablesArguments, VariablesResponse> responder) => _manager.ScheduleRequest(HandleVariablesRequest, responder);
    protected override void HandleDisconnectRequestAsync(IRequestResponder<DisconnectArguments> responder) => _manager.ScheduleRequest(HandleDisconnectRequest, responder);
    protected override void HandleInitializeRequestAsync(IRequestResponder<InitializeArguments, InitializeResponse> responder) => _manager.ScheduleRequest(HandleInitializeRequest, responder);
    protected override void HandleReadMemoryRequestAsync(IRequestResponder<ReadMemoryArguments, ReadMemoryResponse> responder) => _manager.ScheduleRequest(HandleReadMemoryRequest, responder);
    protected override void HandleStackTraceRequestAsync(IRequestResponder<StackTraceArguments, StackTraceResponse> responder) => _manager.ScheduleRequest(HandleStackTraceRequest, responder);
    protected override void HandleCompletionsRequestAsync(IRequestResponder<CompletionsArguments, CompletionsResponse> responder) => _manager.ScheduleRequest(HandleCompletionsRequest, responder);
    protected override void HandleDisassembleRequestAsync(IRequestResponder<DisassembleArguments, DisassembleResponse> responder) => _manager.ScheduleRequest(HandleDisassembleRequest, responder);
    protected override void HandleGotoTargetsRequestAsync(IRequestResponder<GotoTargetsArguments, GotoTargetsResponse> responder) => _manager.ScheduleRequest(HandleGotoTargetsRequest, responder);
    protected override void HandleLoadSymbolsRequestAsync(IRequestResponder<LoadSymbolsArguments> responder) => _manager.ScheduleRequest(HandleLoadSymbolsRequest, responder);
    protected override void HandleSetVariableRequestAsync(IRequestResponder<SetVariableArguments, SetVariableResponse> responder) => _manager.ScheduleRequest(HandleSetVariableRequest, responder);
    protected override void HandleWriteMemoryRequestAsync(IRequestResponder<WriteMemoryArguments, WriteMemoryResponse> responder) => _manager.ScheduleRequest(HandleWriteMemoryRequest, responder);
    protected override void HandleRestartFrameRequestAsync(IRequestResponder<RestartFrameArguments> responder) => _manager.ScheduleRequest(HandleRestartFrameRequest, responder);
    protected override void HandleExceptionInfoRequestAsync(IRequestResponder<ExceptionInfoArguments, ExceptionInfoResponse> responder) => _manager.ScheduleRequest(HandleExceptionInfoRequest, responder);
    protected override void HandleLoadedSourcesRequestAsync(IRequestResponder<LoadedSourcesArguments, LoadedSourcesResponse> responder) => _manager.ScheduleRequest(HandleLoadedSourcesRequest, responder);
    protected override void HandleSetExpressionRequestAsync(IRequestResponder<SetExpressionArguments, SetExpressionResponse> responder) => _manager.ScheduleRequest(HandleSetExpressionRequest, responder);
    protected override void HandleStepInTargetsRequestAsync(IRequestResponder<StepInTargetsArguments, StepInTargetsResponse> responder) => _manager.ScheduleRequest(HandleStepInTargetsRequest, responder);
    protected override void HandleSetBreakpointsRequestAsync(IRequestResponder<SetBreakpointsArguments, SetBreakpointsResponse> responder) => _manager.ScheduleRequest(HandleSetBreakpointsRequest, responder);
    protected override void HandleReverseContinueRequestAsync(IRequestResponder<ReverseContinueArguments> responder) => _manager.ScheduleRequest(HandleReverseContinueRequest, responder);
    protected override void HandleSetSymbolOptionsRequestAsync(IRequestResponder<SetSymbolOptionsArguments> responder) => _manager.ScheduleRequest(HandleSetSymbolOptionsRequest, responder);
    protected override void HandleTerminateThreadsRequestAsync(IRequestResponder<TerminateThreadsArguments> responder) => _manager.ScheduleRequest(HandleTerminateThreadsRequest, responder);
    protected override void HandleConfigurationDoneRequestAsync(IRequestResponder<ConfigurationDoneArguments> responder) => _manager.ScheduleRequest(HandleConfigurationDoneRequest, responder);
    protected override void HandleSetJMCProjectListRequestAsync(IRequestResponder<SetJMCProjectListArguments> responder) => _manager.ScheduleRequest(HandleSetJMCProjectListRequest, responder);
    protected override void HandleSetDataBreakpointsRequestAsync(IRequestResponder<SetDataBreakpointsArguments, SetDataBreakpointsResponse> responder) => _manager.ScheduleRequest(HandleSetDataBreakpointsRequest, responder);
    protected override void HandleDataBreakpointInfoRequestAsync(IRequestResponder<DataBreakpointInfoArguments, DataBreakpointInfoResponse> responder) => _manager.ScheduleRequest(HandleDataBreakpointInfoRequest, responder);
    protected override void HandleBreakpointLocationsRequestAsync(IRequestResponder<BreakpointLocationsArguments, BreakpointLocationsResponse> responder) => _manager.ScheduleRequest(HandleBreakpointLocationsRequest, responder);
    protected override void HandleSetDebuggerPropertyRequestAsync(IRequestResponder<SetDebuggerPropertyArguments> responder) => _manager.ScheduleRequest(HandleSetDebuggerPropertyRequest, responder);
    protected override void HandleModuleSymbolSearchLogRequestAsync(IRequestResponder<ModuleSymbolSearchLogArguments, ModuleSymbolSearchLogResponse> responder) => _manager.ScheduleRequest(HandleModuleSymbolSearchLogRequest, responder);
    protected override void HandleSetFunctionBreakpointsRequestAsync(IRequestResponder<SetFunctionBreakpointsArguments, SetFunctionBreakpointsResponse> responder) => _manager.ScheduleRequest(HandleSetFunctionBreakpointsRequest, responder);
    protected override void HandleSetExceptionBreakpointsRequestAsync(IRequestResponder<SetExceptionBreakpointsArguments, SetExceptionBreakpointsResponse> responder) => _manager.ScheduleRequest(HandleSetExceptionBreakpointsRequest, responder);
    protected override void HandleSetInstructionBreakpointsRequestAsync(IRequestResponder<SetInstructionBreakpointsArguments, SetInstructionBreakpointsResponse> responder) => _manager.ScheduleRequest(HandleSetInstructionBreakpointsRequest, responder);

}
