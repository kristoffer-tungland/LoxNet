namespace LoxNet;

internal static class HexUtils
{
#if NET48
    public static byte[] FromHexString(string hex)
    {
        if (hex == null) throw new System.ArgumentNullException(nameof(hex));
        var result = new byte[hex.Length / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return result;
    }
#else
    public static byte[] FromHexString(string hex) => System.Convert.FromHexString(hex);
#endif
}
