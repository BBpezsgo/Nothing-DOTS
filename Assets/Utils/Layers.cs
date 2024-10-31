public static class Layers
{
    public const uint None = 0u;
    public const uint All = ~0u;

    public const uint Default = 1u << 0;
    public const uint TransparentFX = 1u << 1;
    public const uint IgnoreRaycast = 1u << 2;
    public const uint _Unused = 1u << 3;
    public const uint Water = 1u << 4;
    public const uint UI = 1u << 5;
    public const uint Selectable = 1u << 6;
    public const uint Ground = 1u << 7;
}
