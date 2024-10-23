using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;

public struct NativeExternalFunction : IBufferElementData, IExternalFunction
{
    public FixedString32Bytes Name;
    public int Id;
    public int ParametersSize;
    public int ReturnValueSize;

    readonly string IExternalFunction.Name => Name.ToString();
    readonly int IExternalFunction.Id => Id;
    readonly int IExternalFunction.ParametersSize => ParametersSize;
    readonly int IExternalFunction.ReturnValueSize => ReturnValueSize;
}
