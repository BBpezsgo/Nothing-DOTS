#define _UNITY_PROFILER
#if UNITY_EDITOR && EDITOR_DEBUG
#define DEBUG_LINES
#endif

using System;
using System.Runtime.CompilerServices;
using AOT;
using LanguageCore;
using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Profiling;
using Unity.Transforms;
using System.Runtime.InteropServices;
using Unity.Entities.UniversalDelegates;

public enum UserUIElementType : byte
{
    MIN,
    Label,
    Image,
    MAX,
}

[BurstCompile]
[StructLayout(LayoutKind.Explicit)]
public struct UserUIElement
{
    [FieldOffset(0)] public bool IsDirty;
    [FieldOffset(1)] public int Id;
    [FieldOffset(5)] public UserUIElementType Type;
    [FieldOffset(6)] public int2 Position;
    [FieldOffset(14)] public int2 Size;
    [FieldOffset(22)] public UserUIElementLabel Label;
    [FieldOffset(22)] public UserUIElementImage Image;
}

[BurstCompile]
[StructLayout(LayoutKind.Sequential)]
public struct UserUIElementLabel
{
    public float3 Color;
    public FixedBytes30 Text;
}

[BurstCompile]
[StructLayout(LayoutKind.Sequential)]
public struct UserUIElementImage
{
    public short Width;
    public short Height;
    public FixedBytes510 Image;
}

struct OwnedData<T>
{
    public readonly int Owner;
    public T Value;

    public OwnedData(int owner, T value)
    {
        Owner = owner;
        Value = value;
    }
}

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
unsafe partial struct ProcessorSystemServer : ISystem
{
    public const int CyclesPerTick = 256;

    public static readonly BytecodeInterpreterSettings BytecodeInterpreterSettings = new()
    {
        HeapSize = Processor.HeapSize,
        StackSize = Processor.StackSize,
    };

    [BurstCompile]
    public ref struct ProcessorRef
    {
        public required void* Memory;
        public required int* Crash;
        public required Signal* Signal;
        public required Registers* Registers;

        public readonly Span<byte> MemorySpan => new(Memory, Processor.TotalMemorySize);

        [BurstCompile]
        public void Push(scoped ReadOnlySpan<byte> data)
        {
            Registers->StackPointer += data.Length * ProcessorState.StackDirection;

            if (Registers->StackPointer >= global::Processor.UserMemorySize ||
                Registers->StackPointer < global::Processor.HeapSize)
            {
                *Signal = LanguageCore.Runtime.Signal.StackOverflow;
                return;
            }

            ((nint)Memory).Set(Registers->StackPointer, data);
        }

        [BurstCompile]
        public void DoCrash(in FixedString32Bytes message)
        {
            char* ptr = stackalloc char[message.Length * sizeof(char)];
            Unicode.Utf8ToUtf16(message.GetUnsafePtr(), message.Length, ptr, out int utf16Length, message.Length * sizeof(char));
            Push(new Span<byte>(ptr, utf16Length * sizeof(char)));

            *Crash = Registers->StackPointer;
            *Signal = LanguageCore.Runtime.Signal.UserCrash;
        }

        [BurstCompile]
        public readonly void GetString(int pointer, out FixedString32Bytes @string)
        {
            @string = new();
            for (int i = pointer; i < pointer + 32; i += sizeof(char))
            {
                char c = *(char*)((byte*)Memory + i);
                if (c == '\0') break;
                @string.Append(c);
            }
        }
    }

    [BurstCompile]
    public ref struct EntityRef
    {
        public required Entity Entity;
        public required RefRW<Processor> Processor;
        public required RefRO<LocalToWorld> WorldTransform;
        public required RefRO<LocalTransform> LocalTransform;
        public required RefRO<UnitTeam> Team;
    }

    [BurstCompile]
    public ref struct FunctionScope
    {
        public required NativeList<OwnedData<BufferedLine>>.ParallelWriter DebugLines;
        public required NativeList<OwnedData<BufferedWorldLabel>>.ParallelWriter WorldLabels;
        public required NativeList<OwnedData<UserUIElement>>.ParallelWriter UIElements;
        public required ProcessorRef ProcessorRef;
        public required EntityRef EntityRef;
    }

    NativeArray<ExternalFunctionScopedSync> scopedExternalFunctions;
    NativeList<OwnedData<BufferedLine>> debugLines;
    NativeList<OwnedData<BufferedWorldLabel>> worldLabels;
    NativeList<OwnedData<UserUIElement>> uiElements;

    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WorldLabelSettings>();

        NativeList<ExternalFunctionScopedSync> _scopedExternalFunctions = new(Allocator.Temp);
        ProcessorAPI.GenerateExternalFunctions(ref _scopedExternalFunctions);
        scopedExternalFunctions = new NativeArray<ExternalFunctionScopedSync>(_scopedExternalFunctions.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        scopedExternalFunctions.CopyFrom(_scopedExternalFunctions.AsArray());
        _scopedExternalFunctions.Dispose();

        debugLines = new(256, Allocator.Persistent);
        worldLabels = new(256, Allocator.Persistent);
        uiElements = new(256, Allocator.Persistent);

        // SystemAPI.GetSingleton<RpcCollection>()
        //     .RegisterRpc(ComponentType.ReadWrite<UIElementUpdateRpc>(), default(UIElementUpdateRpc).CompileExecute());
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = default;
        EntityArchetype rpcArchetype = state.EntityManager.CreateArchetype(stackalloc ComponentType[]
        {
            typeof(SendRpcCommandRequest),
            typeof(DebugLineRpc),
        });

        foreach (var (player, lines, labels) in
            SystemAPI.Query<RefRO<Player>, DynamicBuffer<BufferedLine>, DynamicBuffer<BufferedWorldLabel>>())
        {
            Entity connection = Entity.Null;
            foreach (var (_connection, _connectionEntity) in
                SystemAPI.Query<RefRO<NetworkId>>()
                .WithEntityAccess())
            {
                if (_connection.ValueRO.Value != player.ValueRO.ConnectionId) continue;
                connection = _connectionEntity;
                break;
            }

            if (connection != Entity.Null && player.ValueRO.ConnectionState == PlayerConnectionState.Connected)
                for (int i = 0; i < debugLines.Length; i++)
                {
                    if (debugLines[i].Owner != player.ValueRO.Team) continue;

                    if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

                    if (Utils.Distance(player.ValueRO.Position, debugLines[i].Value.Value) < 50f)
                    {
                        Entity rpc = commandBuffer.CreateEntity(rpcArchetype);
                        commandBuffer.SetComponent<SendRpcCommandRequest>(rpc, new()
                        {
                            TargetConnection = connection,
                        });
                        commandBuffer.SetComponent<DebugLineRpc>(rpc, new()
                        {
                            Position = debugLines[i].Value.Value,
                            Color = debugLines[i].Value.Color,
                        });
                    }

                    // for (int j = 0; j < lines.Length; j++)
                    // {
                    //     if (debugLines[i].Value.Value.Equals(lines[j].Value))
                    //     {
                    //         lines.Set(j, debugLines[i].Value);
                    //         goto next;
                    //     }
                    // }
                    // lines.Add(debugLines[i].Value);
                    // next:;
                }

            for (int i = 0; i < worldLabels.Length; i++)
            {
                if (worldLabels[i].Owner != player.ValueRO.Team) continue;

                for (int j = 0; j < labels.Length; j++)
                {
                    if (worldLabels[i].Value.Position.Equals(labels[j].Position))
                    {
                        labels.Set(j, worldLabels[i].Value);
                        goto next;
                    }
                }
                labels.Add(worldLabels[i].Value);
            next:;
            }

            for (int i = 0; i < uiElements.Length; i++)
            {
                if (uiElements[i].Owner != player.ValueRO.Team) continue;
                if (!uiElements[i].Value.IsDirty && uiElements[i].Value.Id != 0) continue;

                if (connection == Entity.Null) continue;

                if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

                if (uiElements[i].Value.Id == 0)
                {
                    // Debug.Log(string.Format("{0} destroyed", uiElements[i]));

                    Entity rpc = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent<SendRpcCommandRequest>(rpc, new()
                    {
                        TargetConnection = connection,
                    });
                    commandBuffer.AddComponent<UIElementDestroyRpc>(rpc, new()
                    {
                        Id = uiElements[i].Value.Id,
                    });
                    uiElements.RemoveAt(i--);
                }
                else
                {
                    // Debug.Log(string.Format("{0} updated, {1}", uiElements[i], uiElements[i].Value.Label.Text.AsString()));

                    Entity rpc = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent<SendRpcCommandRequest>(rpc, new()
                    {
                        TargetConnection = connection,
                    });
                    commandBuffer.AddComponent<UIElementUpdateRpc>(rpc, new()
                    {
                        UIElement = uiElements[i].Value,
                    });
                    uiElements.AsArray().AsSpan()[i].Value.IsDirty = false;
                }
            }
        }

        debugLines.Clear();
        worldLabels.Clear();

        new ProcessorJob()
        {
            scopedExternalFunctions = scopedExternalFunctions,

            debugLines = debugLines.AsParallelWriter(),
            worldLabels = worldLabels.AsParallelWriter(),
            uiElements = uiElements.AsParallelWriter(),

            QCoreComputer = SystemAPI.GetComponentLookup<CoreComputer>(true),
            QRadar = SystemAPI.GetComponentLookup<Radar>(true),
            QFacility = SystemAPI.GetComponentLookup<Facility>(true),
        }.ScheduleParallel();
    }
}

[BurstCompile(CompileSynchronously = true)]
[WithAll(typeof(Processor), typeof(UnitTeam), typeof(LocalToWorld), typeof(LocalTransform))]
partial struct ProcessorJob : IJobEntity
{
    [ReadOnly] public NativeArray<ExternalFunctionScopedSync> scopedExternalFunctions;
    public NativeList<OwnedData<BufferedLine>>.ParallelWriter debugLines;
    public NativeList<OwnedData<BufferedWorldLabel>>.ParallelWriter worldLabels;
    public NativeList<OwnedData<UserUIElement>>.ParallelWriter uiElements;
    [ReadOnly] public ComponentLookup<CoreComputer> QCoreComputer;
    [ReadOnly] public ComponentLookup<Radar> QRadar;
    [ReadOnly] public ComponentLookup<Facility> QFacility;

    [BurstCompile(CompileSynchronously = true)]
    unsafe void Execute(
        RefRW<Processor> processor,
        RefRO<UnitTeam> team,
        RefRO<LocalToWorld> worldTransform,
        RefRO<LocalTransform> localTransform,
        DynamicBuffer<BufferedInstruction> code,
        DynamicBuffer<BufferedGeneratedFunction> generatedFunctions,
        Entity entity)
    {
        if (processor.ValueRO.SourceFile == default)
        {
            processor.ValueRW.StatusLED.Status = 0;
            return;
        }

        if (code.IsEmpty)
        {
            processor.ValueRW.StatusLED.Status = 0;
            return;
        }

        MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);
        mapped->Pendrive.IsPlugged = processor.ValueRW.PluggedPendrive.Entity != Entity.Null;

        ProcessorSystemServer.FunctionScope scope = new()
        {
            DebugLines = debugLines,
            WorldLabels = worldLabels,
            UIElements = uiElements,
            ProcessorRef = new ProcessorSystemServer.ProcessorRef()
            {
                Memory = Unsafe.AsPointer(ref processor.ValueRW.Memory),
                Crash = null,
                Registers = null,
                Signal = null,
            },
            EntityRef = new ProcessorSystemServer.EntityRef()
            {
                Entity = entity,
                Processor = processor,
                WorldTransform = worldTransform,
                LocalTransform = localTransform,
                Team = team,
            },
        };

        NativeList<ExternalFunctionScopedSync> scopedExternalFunctions = new(this.scopedExternalFunctions.Length + generatedFunctions.Length, Allocator.Temp);

        for (int i = 0; i < this.scopedExternalFunctions.Length; i++)
        {
            if ((this.scopedExternalFunctions[i].Id & ProcessorAPI.GlobalPrefix) == ProcessorAPI.GUI.Prefix &&
                !QCoreComputer.HasComponent(entity))
            {
                continue;
            }

            scopedExternalFunctions.Add(this.scopedExternalFunctions[i]);
            scopedExternalFunctions.GetUnsafePtr()[scopedExternalFunctions.Length - 1].Scope = (nint)(void*)&scope;
        }

        for (int i = 0; i < generatedFunctions.Length; i++)
        {
            scopedExternalFunctions.Add(generatedFunctions[i].V);
        }

        ProcessorState processorState = new(
            ProcessorSystemServer.BytecodeInterpreterSettings,
            processor.ValueRW.Registers,
            new Span<byte>(Unsafe.AsPointer(ref processor.ValueRW.Memory), Processor.TotalMemorySize),
            new ReadOnlySpan<Instruction>(code.GetUnsafeReadOnlyPtr(), code.Length),
            scopedExternalFunctions.AsReadOnly()
        )
        {
            Signal = processor.ValueRO.Signal,
            Crash = processor.ValueRO.Crash,
        };

        scope.ProcessorRef.Crash = &processorState.Crash;
        scope.ProcessorRef.Signal = &processorState.Signal;
        scope.ProcessorRef.Registers = &processorState.Registers;

        for (int i = 0; i < ProcessorSystemServer.CyclesPerTick; i++)
        {
            if (processorState.Signal != Signal.None)
            {
                if (!processor.ValueRO.SignalNotified)
                {
                    processor.ValueRW.SignalNotified = true;
                    switch (processorState.Signal)
                    {
                        case Signal.UserCrash:
                            Debug.LogError(string.Format("Crashed ({0})", processorState.Crash));
                            break;
                        case Signal.StackOverflow:
                            Debug.LogError("Stack Overflow");
                            break;
                        case Signal.Halt:
                            // Debug.LogError("Halted");
                            break;
                        case Signal.UndefinedExternalFunction:
                            Debug.LogError(string.Format("Undefined external function {0}", processorState.Crash));
                            break;
                        case Signal.PointerOutOfRange:
                            Debug.LogError("Pointer out of Range");
                            break;
                    }
                }
                break;
            }
            processor.ValueRW.SignalNotified = false;

            if (processor.ValueRO.IsKeyRequested)
            {
                if (processor.ValueRO.InputKey.Length == 0) break;
                char key = processor.ValueRW.InputKey[0];
                processor.ValueRW.InputKey.RemoveAt(0);
                processor.ValueRW.IsKeyRequested = false;
                processorState.Pop(2);
                processorState.Push(key.AsBytes());
            }

            processorState.Process();
        }

        processor.ValueRW.Registers = processorState.Registers;
        processor.ValueRW.Signal = processorState.Signal;
        processor.ValueRW.Crash = processorState.Crash;
        processor.ValueRW.StatusLED.Status = processorState.Signal == Signal.None ? 1 : 2;

        scopedExternalFunctions.Dispose();
    }
}
