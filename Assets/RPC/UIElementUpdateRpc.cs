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
        writer.WriteByte((byte)data.UIElement.Type);
        writer.WriteInt(data.UIElement.Position.x);
        writer.WriteInt(data.UIElement.Position.y);
        writer.WriteInt(data.UIElement.Size.x);
        writer.WriteInt(data.UIElement.Size.y);

        switch (data.UIElement.Type)
        {
            case UserUIElementType.Label:
                writer.WritePackedFloat(data.UIElement.Label.Color.x, state.CompressionModel);
                writer.WritePackedFloat(data.UIElement.Label.Color.y, state.CompressionModel);
                writer.WritePackedFloat(data.UIElement.Label.Color.z, state.CompressionModel);
                fixed (void* ptr = &data.UIElement.Label.Text)
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
                writer.WriteShort(data.UIElement.Image.Width);
                writer.WriteShort(data.UIElement.Image.Height);
                int l = Math.Clamp(data.UIElement.Image.Width * data.UIElement.Image.Height, 1, 510);
                fixed (void* ptr = &data.UIElement.Image.Image)
                {
                    writer.WriteBytes(new Span<byte>(ptr, l));
                }
                break;
        }
    }

    [BurstCompile]
    public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref UIElementUpdateRpc data)
    {
        data.UIElement.Id = reader.ReadInt();
        data.UIElement.Type = (UserUIElementType)reader.ReadByte();
        data.UIElement.Position.x = reader.ReadInt();
        data.UIElement.Position.y = reader.ReadInt();
        data.UIElement.Size.x = reader.ReadInt();
        data.UIElement.Size.y = reader.ReadInt();

        switch (data.UIElement.Type)
        {
            case UserUIElementType.Label:
                data.UIElement.Label.Color.x = reader.ReadPackedFloat(state.CompressionModel);
                data.UIElement.Label.Color.y = reader.ReadPackedFloat(state.CompressionModel);
                data.UIElement.Label.Color.z = reader.ReadPackedFloat(state.CompressionModel);
                fixed (void* ptr = &data.UIElement.Label.Text)
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
                data.UIElement.Image.Width = reader.ReadShort();
                data.UIElement.Image.Height = reader.ReadShort();
                int l = Math.Clamp(data.UIElement.Image.Width * data.UIElement.Image.Height, 1, 510);
                fixed (void* ptr = &data.UIElement.Image.Image)
                {
                    reader.ReadBytes(new Span<byte>(ptr, l));
                }
                break;
        }
    }

    public readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute() => new(InvokeExecute);
    [BurstCompile(DisableDirectCall = true)]
    static void InvokeExecute(ref RpcExecutor.Parameters parameters) => RpcExecutor.ExecuteCreateRequestComponent<UIElementUpdateRpc, UIElementUpdateRpc>(ref parameters);
}
