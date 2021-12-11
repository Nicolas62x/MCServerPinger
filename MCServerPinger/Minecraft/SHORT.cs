struct SHORT
{
    public static short readShort(IEnumerable<byte> data, ref int ptr)
    {
        ptr += 2;
        return readShort(data, ptr - 2);
    }

    public static short readShort(IEnumerable<byte> data, int idx = 0)
    {
        if (data.Count() >= idx + 2)
            return (short)((data.ElementAt(idx) << 8) | (data.ElementAt(idx + 1)));
        else
            throw new System.Exception($"Data.Count() = {data.Count()}, where at least {idx + 2} is expected");
    }
    public static ushort readUShort(IEnumerable<byte> data, ref int ptr)
    {
        ptr += 2;
        return readUShort(data, ptr - 2);
    }

    public static ushort readUShort(IEnumerable<byte> data, int idx = 0)
    {
        if (data.Count() >= idx + 2)
            return (ushort)((data.ElementAt(idx) << 8) | (data.ElementAt(idx + 1)));
        else
            throw new System.Exception($"Data.Count() = {data.Count()}, where at least {idx + 2} is expected");
    }

    public static byte[] GetData(short s)
    {
        byte[] res = new byte[2];

        res[0] = (byte)((s & 0xff00) >> 8);
        res[1] = (byte)((s & 0xff));

        return res;
    }

    public static byte[] GetData(ushort s)
    {
        byte[] res = new byte[2];

        res[0] = (byte)((s & 0xff00) >> 8);
        res[1] = (byte)((s & 0xff));

        return res;
    }
}