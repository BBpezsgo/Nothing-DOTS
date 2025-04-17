#define UNITY_PROFILER

#if UNITY_EDITOR && EDITOR_DEBUG
#define DEBUG_LINES
#endif

using System;
using System.Runtime.CompilerServices;
using AOT;
using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using FunctionScope = ProcessorSystemServer.FunctionScope;

[BurstCompile]
static unsafe class ProcessorAPI
{
    [BurstCompile]
    public static class Math
    {
        public const int Prefix = 0x00010000;

        public static readonly Unity.Mathematics.Random SharedRandom = Unity.Mathematics.Random.CreateFromIndex(420);
        static readonly ProfilerMarker MarkerRandom = new("ProcessorSystemServer.External.other");

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Atan2(nint scope, nint arguments, nint returnValue)
        {
            (float a, float b) = ExternalFunctionGenerator.TakeParameters<float, float>(arguments);
            float r = math.atan2(a, b);
            returnValue.Set(r);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Sin(nint scope, nint arguments, nint returnValue)
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.sin(a);
            returnValue.Set(r);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Cos(nint scope, nint arguments, nint returnValue)
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.cos(a);
            returnValue.Set(r);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Tan(nint scope, nint arguments, nint returnValue)
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.tan(a);
            returnValue.Set(r);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Asin(nint scope, nint arguments, nint returnValue)
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.asin(a);
            returnValue.Set(r);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Acos(nint scope, nint arguments, nint returnValue)
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.acos(a);
            returnValue.Set(r);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Atan(nint scope, nint arguments, nint returnValue)
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.atan(a);
            returnValue.Set(r);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Sqrt(nint scope, nint arguments, nint returnValue)
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.sqrt(a);
            returnValue.Set(r);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Random(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerRandom.Auto();
#endif

            returnValue.Set(SharedRandom.NextInt());
        }
    }

    [BurstCompile]
    public static class IO
    {
        public const int Prefix = 0x00020000;

        static readonly ProfilerMarker MarkerStdout = new("ProcessorSystemServer.External.stdout");

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void StdOut(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerStdout.Auto();
#endif

            char output = arguments.To<char>();
            if (output == '\r') return;
            ((FunctionScope*)_scope)->EntityRef.Processor.ValueRW.StdOutBuffer.AppendShift(output);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void StdIn(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            // using ProfilerMarker.AutoScope marker = _ExternalMarker_stdout.Auto();
#endif

            ((FunctionScope*)_scope)->EntityRef.Processor.ValueRW.IsKeyRequested = true;
        }
    }

    [BurstCompile]
    public static class Transmission
    {
        public const int Prefix = 0x00030000;

        static readonly ProfilerMarker MarkerSend = new("ProcessorSystemServer.External.send");
        static readonly ProfilerMarker MarkerReceive = new("ProcessorSystemServer.External.receive");

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Send(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerSend.Auto();
#endif

            FunctionScope* scope = (FunctionScope*)_scope;

            (int bufferPtr, int length, float directionAngle, float angle) = ExternalFunctionGenerator.TakeParameters<int, int, float, float>(arguments);
            if (length <= 0 || length >= 30) throw new Exception("Passed buffer length must be in range [0,30] inclusive");
            if (bufferPtr == 0) throw new Exception($"Passed buffer pointer is null");
            if (bufferPtr < 0 || bufferPtr + length >= Processor.UserMemorySize) throw new Exception($"Passed buffer pointer is invalid");

            float3 direction;
            if (angle != 0f)
            {
                direction.x = math.cos(directionAngle);
                direction.y = 0f;
                direction.z = math.sin(directionAngle);
                direction = scope->EntityRef.LocalTransform.ValueRO.TransformDirection(direction);
            }
            else
            {
                direction = default;
            }
            float cosAngle = math.abs(math.cos(angle));

            FixedList32Bytes<byte> data = new();
            data.AddRange((byte*)((nint)scope->ProcessorRef.Memory + bufferPtr), length);

            if (scope->EntityRef.Processor.ValueRW.OutgoingTransmissions.Length >= scope->EntityRef.Processor.ValueRW.OutgoingTransmissions.Capacity)
            { scope->EntityRef.Processor.ValueRW.OutgoingTransmissions.RemoveAt(0); }
            scope->EntityRef.Processor.ValueRW.OutgoingTransmissions.Add(new()
            {
                Source = scope->EntityRef.WorldTransform.ValueRO.Position,
                Direction = direction,
                Data = data,
                CosAngle = cosAngle,
                Angle = angle,
            });
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Receive(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerReceive.Auto();
#endif

            returnValue.Set(0);

            (int bufferPtr, int length, int directionPtr) = ExternalFunctionGenerator.TakeParameters<int, int, int>(arguments);
            if (bufferPtr == 0 || length <= 0) return;
            if (bufferPtr < 0 || bufferPtr + length >= Processor.UserMemorySize) throw new Exception($"Passed buffer pointer is invalid");

            FunctionScope* scope = (FunctionScope*)_scope;

            ref FixedList128Bytes<BufferedUnitTransmission> received = ref scope->EntityRef.Processor.ValueRW.IncomingTransmissions; // scope->EntityManager.GetBuffer<BufferedUnitTransmission>(scope->SourceEntity);
            if (received.Length == 0) return;

            BufferedUnitTransmission first = received[0];

            int copyLength = math.min(first.Data.Length, length);

            Buffer.MemoryCopy(((byte*)&first.Data) + 2, (byte*)scope->ProcessorRef.Memory + bufferPtr, copyLength, copyLength);

            if (directionPtr > 0)
            {
                float3 transformed = scope->EntityRef.LocalTransform.ValueRO.InverseTransformPoint(first.Source);
                transformed = math.normalize(transformed);
                Span<byte> memory = new(scope->ProcessorRef.Memory, Processor.UserMemorySize);
                memory.Set(directionPtr, math.atan2(transformed.z, transformed.x));
            }

            if (copyLength >= first.Data.Length)
            {
                received.RemoveAt(0);
            }
            else
            {
                first.Data.RemoveRange(0, copyLength);
                received[0] = first;
            }

            returnValue.Set(copyLength);
        }
    }

    [BurstCompile]
    public static class Commands
    {
        public const int Prefix = 0x00040000;

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Dequeue(nint _scope, nint arguments, nint returnValue)
        {
            returnValue.Set(0);

            int dataPtr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
            if (dataPtr == 0) return;
            if (dataPtr < 0 || dataPtr >= Processor.UserMemorySize) throw new Exception($"Passed data pointer is invalid");

            FunctionScope* scope = (FunctionScope*)_scope;

            ref FixedList128Bytes<UnitCommandRequest> queue = ref scope->EntityRef.Processor.ValueRW.CommandQueue; // scope->EntityManager.GetBuffer<BufferedUnitTransmission>(scope->SourceEntity);
            if (queue.Length == 0) return;

            UnitCommandRequest first = queue[0];
            queue.RemoveAt(0);

            Buffer.MemoryCopy(&first.Data, (byte*)scope->ProcessorRef.Memory + dataPtr, first.DataLength, first.DataLength);

            returnValue.Set(first.Id);
        }
    }

    [BurstCompile]
    public static class Debug
    {
        public const int Prefix = 0x00050000;

        static readonly ProfilerMarker MarkerDebug = new("ProcessorSystemServer.External.debug");

        const float DebugLineDuration = 0.5f;

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Line(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerDebug.Auto();
#endif

            (float3 position, byte color) = ExternalFunctionGenerator.TakeParameters<float3, byte>(arguments);

            FunctionScope* scope = (FunctionScope*)_scope;

            if (scope->DebugLines.ListData->Length + 1 < scope->DebugLines.ListData->Capacity) scope->DebugLines.AddNoResize(new(
                scope->EntityRef.Team.ValueRO.Team,
                new BufferedLine(new float3x2(
                    scope->EntityRef.WorldTransform.ValueRO.Position,
                    position
                ), color, MonoTime.Now + DebugLineDuration)
            ));

#if DEBUG_LINES
            UnityEngine.Debug.DrawLine(
                scope->EntityRef.WorldTransform.ValueRO.Position,
                position,
                new Color(
                    (color & 0b100) != 0 ? 1f : 0f,
                    (color & 0b010) != 0 ? 1f : 0f,
                    (color & 0b001) != 0 ? 1f : 0f
                ),
                DebugLineDuration);
#endif
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void LineL(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerDebug.Auto();
#endif

            (float3 position, byte color) = ExternalFunctionGenerator.TakeParameters<float3, byte>(arguments);

            FunctionScope* scope = (FunctionScope*)_scope;

            RefRO<LocalTransform> transform = scope->EntityRef.LocalTransform;
            float3 transformed = transform.ValueRO.TransformPoint(position);

            if (scope->DebugLines.ListData->Length + 1 < scope->DebugLines.ListData->Capacity) scope->DebugLines.AddNoResize(new(
                scope->EntityRef.Team.ValueRO.Team,
                new BufferedLine(new float3x2(
                    scope->EntityRef.WorldTransform.ValueRO.Position,
                    transformed
                ), color, MonoTime.Now + DebugLineDuration)
            ));

#if DEBUG_LINES
            UnityEngine.Debug.DrawLine(
                scope->EntityRef.WorldTransform.ValueRO.Position,
                transformed,
                new Color(
                    (color & 0b100) != 0 ? 1f : 0f,
                    (color & 0b010) != 0 ? 1f : 0f,
                    (color & 0b001) != 0 ? 1f : 0f
                ),
                DebugLineDuration);
#endif
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Label(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerDebug.Auto();
#endif

            (float3 position, int textPtr) = ExternalFunctionGenerator.TakeParameters<float3, int>(arguments);

            FunctionScope* scope = (FunctionScope*)_scope;

            FixedString32Bytes text = new();
            for (int i = textPtr; i < textPtr + 32; i += sizeof(char))
            {
                char c = *(char*)((byte*)scope->ProcessorRef.Memory + i);
                if (c == '\0') break;
                text.Append(c);
            }

            if (scope->WorldLabels.ListData->Length + 1 < scope->WorldLabels.ListData->Capacity) scope->WorldLabels.AddNoResize(new(
                scope->EntityRef.Team.ValueRO.Team,
                new BufferedWorldLabel(position, 0b111, text, MonoTime.Now + DebugLineDuration)
            ));
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void LabelL(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerDebug.Auto();
#endif

            (float3 position, int textPtr) = ExternalFunctionGenerator.TakeParameters<float3, int>(arguments);

            FunctionScope* scope = (FunctionScope*)_scope;

            FixedString32Bytes text = new();
            for (int i = textPtr; i < textPtr + 32; i += sizeof(char))
            {
                char c = *(char*)((byte*)scope->ProcessorRef.Memory + i);
                if (c == '\0') break;
                text.Append(c);
            }

            float3 transformed = scope->EntityRef.LocalTransform.ValueRO.TransformPoint(position);

            if (scope->WorldLabels.ListData->Length + 1 < scope->WorldLabels.ListData->Capacity) scope->WorldLabels.AddNoResize(new(
                scope->EntityRef.Team.ValueRO.Team,
                new BufferedWorldLabel(transformed, 0b111, text, MonoTime.Now + DebugLineDuration)
            ));
        }
    }

    [BurstCompile]
    public static class Environment
    {
        public const int Prefix = 0x00060000;

        static readonly ProfilerMarker MarkerTime = new("ProcessorSystemServer.External.time");
        static readonly ProfilerMarker MarkerToGlobal = new("ProcessorSystemServer.External.toglobal");
        static readonly ProfilerMarker MarkerToLocal = new("ProcessorSystemServer.External.tolocal");

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void ToGlobal(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerToGlobal.Auto();
#endif

            int ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
            if (ptr <= 0 || ptr <= 0) return;

            FunctionScope* scope = (FunctionScope*)_scope;
            Span<byte> memory = new(scope->ProcessorRef.Memory, Processor.UserMemorySize);
            float3 point = memory.Get<float3>(ptr);
            RefRO<LocalTransform> transform = scope->EntityRef.LocalTransform;
            float3 transformed = transform.ValueRO.TransformPoint(point);
            memory.Set(ptr, transformed);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void ToLocal(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerToLocal.Auto();
#endif

            int ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
            if (ptr <= 0 || ptr <= 0) return;

            FunctionScope* scope = (FunctionScope*)_scope;
            Span<byte> memory = new(scope->ProcessorRef.Memory, Processor.UserMemorySize);
            float3 point = memory.Get<float3>(ptr);
            RefRO<LocalTransform> transform = scope->EntityRef.LocalTransform;
            float3 transformed = transform.ValueRO.InverseTransformPoint(point);
            memory.Set(ptr, transformed);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Time(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerTime.Auto();
#endif

            FunctionScope* scope = (FunctionScope*)_scope;
            returnValue.Set(MonoTime.Now);
        }
    }

    [BurstCompile]
    public static class Sensors
    {
        public const int Prefix = 0x00070000;

        static readonly ProfilerMarker MarkerRadar = new("ProcessorSystemServer.External.radar");

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Radar(nint _scope, nint arguments, nint returnValue)
        {
#if UNITY_PROFILER
            using ProfilerMarker.AutoScope marker = MarkerRadar.Auto();
#endif

            FunctionScope* scope = (FunctionScope*)_scope;

            MappedMemory* mapped = (MappedMemory*)((nint)scope->ProcessorRef.Memory + Processor.MappedMemoryStart);

            float3 direction;
            direction.x = math.cos(mapped->Radar.RadarDirection);
            direction.y = 0f;
            direction.z = math.sin(mapped->Radar.RadarDirection);

            scope->EntityRef.Processor.ValueRW.RadarResponse = 0f;
            scope->EntityRef.Processor.ValueRW.RadarRequest = direction;
        }
    }

    [BurstCompile]
    public static class GUI
    {
        public const int Prefix = 0x00080000;

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Create(nint _scope, nint arguments, nint returnValue)
        {
            int _ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
            FunctionScope* scope = (FunctionScope*)_scope;

            UserUIElement* ptr = (UserUIElement*)((nint)scope->ProcessorRef.Memory + _ptr);

            int id = 1;
            while (true)
            {
                bool exists = false;

                for (int i = 0; i < scope->UIElements.ListData->Length; i++)
                {
                    if ((*scope->UIElements.ListData)[i].Value.Id != id) continue;
                    if ((*scope->UIElements.ListData)[i].Owner != scope->EntityRef.Team.ValueRO.Team) continue;
                    exists = true;
                    break;
                }

                if (!exists) break;
                id++;
            }

            switch (ptr->Type)
            {
                case UserUIElementType.Label:
                    char* text = (char*)&ptr->Label.Text;
                    scope->UIElements.AddNoResize(new(
                        scope->EntityRef.Team.ValueRO.Team,
                        *ptr = new UserUIElement()
                        {
                            IsDirty = true,
                            Type = UserUIElementType.Label,
                            Id = id,
                            Position = ptr->Position,
                            Size = ptr->Size,
                            Label = new UserUIElementLabel()
                            {
                                Color = ptr->Label.Color,
                                Text = ptr->Label.Text,
                            },
                        }
                    ));
                    break;
                case UserUIElementType.Image:
                    scope->UIElements.AddNoResize(new(
                        scope->EntityRef.Team.ValueRO.Team,
                        *ptr = new UserUIElement()
                        {
                            IsDirty = true,
                            Type = UserUIElementType.Image,
                            Id = id,
                            Position = ptr->Position,
                            Size = ptr->Size,
                            Image = new UserUIElementImage()
                            {
                                Width = ptr->Image.Width,
                                Height = ptr->Image.Height,
                                Image = ptr->Image.Image,
                            },
                        }
                    ));
                    break;
                case UserUIElementType.MIN:
                case UserUIElementType.MAX:
                default:
                    break;
            }
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Destroy(nint _scope, nint arguments, nint returnValue)
        {
            int id = ExternalFunctionGenerator.TakeParameters<int>(arguments);
            FunctionScope* scope = (FunctionScope*)_scope;

            for (int i = 0; i < scope->UIElements.ListData->Length; i++)
            {
                if ((*scope->UIElements.ListData)[i].Value.Id != id) continue;
                if ((*scope->UIElements.ListData)[i].Owner != scope->EntityRef.Team.ValueRO.Team) continue;
                (*scope->UIElements.ListData)[i] = default;
                break;
            }
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Update(nint _scope, nint arguments, nint returnValue)
        {
            int _ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
            FunctionScope* scope = (FunctionScope*)_scope;

            UserUIElement* ptr = (UserUIElement*)((nint)scope->ProcessorRef.Memory + _ptr);

            for (int i = 0; i < scope->UIElements.ListData->Length; i++)
            {
                ref OwnedData<UserUIElement> uiElement = ref (*scope->UIElements.ListData).Ptr[i];
                if (uiElement.Value.Id != ptr->Id) continue;
                if (uiElement.Owner != scope->EntityRef.Team.ValueRO.Team) continue;
                switch (ptr->Type)
                {
                    case UserUIElementType.Label:
                        char* text = (char*)&ptr->Label.Text;
                        uiElement = new OwnedData<UserUIElement>(
                            scope->EntityRef.Team.ValueRO.Team,
                            *ptr = new UserUIElement()
                            {
                                IsDirty = true,
                                Type = UserUIElementType.Label,
                                Id = ptr->Id,
                                Position = ptr->Position,
                                Size = ptr->Size,
                                Label = ptr->Label,
                            }
                        );
                        break;
                    case UserUIElementType.Image:
                        uiElement = new OwnedData<UserUIElement>(
                            scope->EntityRef.Team.ValueRO.Team,
                            *ptr = new UserUIElement()
                            {
                                IsDirty = true,
                                Type = UserUIElementType.Image,
                                Id = ptr->Id,
                                Position = ptr->Position,
                                Size = ptr->Size,
                                Image = ptr->Image,
                            }
                        );
                        break;
                }
                break;
            }
        }
    }

    [BurstCompile]
    public static class Pendrive
    {
        public const int Prefix = 0x00090000;

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void TryPlug(nint _scope, nint arguments, nint returnValue)
        {
            FunctionScope* scope = (FunctionScope*)_scope;
            scope->EntityRef.Processor.ValueRW.PendrivePlugRequested = true;
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void TryUnplug(nint _scope, nint arguments, nint returnValue)
        {
            FunctionScope* scope = (FunctionScope*)_scope;
            scope->EntityRef.Processor.ValueRW.PendriveUnplugRequested = true;
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Read(nint _scope, nint arguments, nint returnValue)
        {
            (int source, int destination, int length) = ExternalFunctionGenerator.TakeParameters<int, int, int>(arguments);
            FunctionScope* scope = (FunctionScope*)_scope;

            if (!scope->EntityRef.Processor.ValueRW.IsPendrivePlugged || source < 0 || source >= 1024 || destination <= 0 || length <= 0 || length > 1024)
            {
                return;
            }

            length = math.min(length, 1024 - source);
            byte* sourcePtr = (byte*)Unsafe.AsPointer(ref scope->EntityRef.Processor.ValueRW.PluggedPendrive.Data);
            Buffer.MemoryCopy(sourcePtr + source, (byte*)scope->ProcessorRef.Memory + destination, Processor.TotalMemorySize - destination, length);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
        public static void Write(nint _scope, nint arguments, nint returnValue)
        {
            (int source, int destination, int length) = ExternalFunctionGenerator.TakeParameters<int, int, int>(arguments);
            FunctionScope* scope = (FunctionScope*)_scope;

            if (!scope->EntityRef.Processor.ValueRW.IsPendrivePlugged || destination < 0 || destination >= 1024 || source <= 0 || length <= 0 || length > 1024)
            {
                return;
            }

            length = math.min(length, 1024 - source);
            byte* destinationPtr = (byte*)Unsafe.AsPointer(ref scope->EntityRef.Processor.ValueRW.PluggedPendrive.Data);
            Buffer.MemoryCopy((byte*)scope->ProcessorRef.Memory + source, destinationPtr + destination, 1024 - destination, length);
        }
    }
}
