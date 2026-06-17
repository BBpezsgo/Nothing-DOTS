using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
unsafe struct UIElementUpdateRpc : IComponentData, IRpcCommandSerializer<UIElementUpdateRpc>
{
    public required UserUIElement UIElement;

    [BurstCompile]
    public readonly void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in UIElementUpdateRpc data)
    {
        writer.WriteInt(data.UIElement.Id);
        writer.WriteInt(data.UIElement.Parent);
        writer.WriteInt(data.UIElement.Index);
        writer.WriteInt((byte)data.UIElement.Direction);
        writer.WriteInt(data.UIElement.Margin);
        writer.WriteInt(data.UIElement.Padding);
        writer.WriteInt(data.UIElement.Size.x);
        writer.WriteInt(data.UIElement.Size.y);
        writer.WriteByte((byte)data.UIElement.Type);

        switch (data.UIElement.Type)
        {
            case UserUIElementType.Box:
                break;
            case UserUIElementType.Label:
                writer.WritePackedFloat(data.UIElement.Meta.Label.Color.x, state.CompressionModel);
                writer.WritePackedFloat(data.UIElement.Meta.Label.Color.y, state.CompressionModel);
                writer.WritePackedFloat(data.UIElement.Meta.Label.Color.z, state.CompressionModel);
                fixed (void* ptr = &data.UIElement.Meta.Label.Text)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        char c = ((char*)ptr)[i];
                        writer.WriteByte((byte)c);
                        if (c == '\0') break;
                    }
                }
                break;
            case UserUIElementType.Image:
                writer.WriteShort(data.UIElement.Meta.Image.Width);
                writer.WriteShort(data.UIElement.Meta.Image.Height);
                int l = Math.Clamp(data.UIElement.Meta.Image.Width * data.UIElement.Meta.Image.Height, 1, 510);
                fixed (void* ptr = &data.UIElement.Meta.Image.Image)
                {
                    writer.WriteBytes(new Span<byte>(ptr, l));
                }
                break;
            case UserUIElementType.MIN:
            case UserUIElementType.MAX:
            default:
                throw new UnreachableException();
        }
    }

    [BurstCompile]
    public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref UIElementUpdateRpc data)
    {
        data.UIElement.Id = reader.ReadInt();
        data.UIElement.Parent = reader.ReadInt();
        data.UIElement.Index = reader.ReadInt();
        data.UIElement.Direction = (UserUIDirection)reader.ReadByte();
        data.UIElement.Margin = reader.ReadInt();
        data.UIElement.Padding = reader.ReadInt();
        data.UIElement.Size.x = reader.ReadInt();
        data.UIElement.Size.y = reader.ReadInt();
        data.UIElement.Type = (UserUIElementType)reader.ReadByte();

        switch (data.UIElement.Type)
        {
            case UserUIElementType.Box:
                break;
            case UserUIElementType.Label:
                data.UIElement.Meta.Label.Color.x = reader.ReadPackedFloat(state.CompressionModel);
                data.UIElement.Meta.Label.Color.y = reader.ReadPackedFloat(state.CompressionModel);
                data.UIElement.Meta.Label.Color.z = reader.ReadPackedFloat(state.CompressionModel);
                fixed (void* ptr = &data.UIElement.Meta.Label.Text)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        char c = (char)reader.ReadByte();
                        ((char*)ptr)[i] = c;
                        if (c == '\0') break;
                    }
                }
                break;
            case UserUIElementType.Image:
                data.UIElement.Meta.Image.Width = reader.ReadShort();
                data.UIElement.Meta.Image.Height = reader.ReadShort();
                int l = Math.Clamp(data.UIElement.Meta.Image.Width * data.UIElement.Meta.Image.Height, 1, 510);
                fixed (void* ptr = &data.UIElement.Meta.Image.Image)
                {
                    reader.ReadBytes(new Span<byte>(ptr, l));
                }
                break;
            case UserUIElementType.MIN:
            case UserUIElementType.MAX:
            default:
                throw new UnreachableException();
        }
    }

    public readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute() => new(InvokeExecute);
    [BurstCompile(DisableDirectCall = true)]
    static void InvokeExecute(ref RpcExecutor.Parameters parameters) => RpcExecutor.ExecuteCreateRequestComponent<UIElementUpdateRpc, UIElementUpdateRpc>(ref parameters);
}
