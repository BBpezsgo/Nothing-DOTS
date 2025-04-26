using Unity.Collections;

public static unsafe class FixedBytes
{
    static bool AreEqual(byte* a, byte* b, int length)
    {
        for (int i = 0; i < length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    public static bool AreEqual(FixedBytes30 a, FixedBytes30 b) => AreEqual((byte*)&a, (byte*)&b, 30);
}