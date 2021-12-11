
struct VARINT
{
    public int value;
    public bool finished;
    public int numRead;

    public static VARINT operator +(VARINT v, int a)
    {
        if (a < 0 || a > 255)
            throw new Exception("Invalid varint input: " + a);

        return v + (byte)a;
    }
    public static VARINT operator +(VARINT v, byte a)
    {
        v.read(a);
        return v;
    }

    public bool read(byte a)
    {
        if (finished)
            throw new Exception("VarInt is allready finished");

        value |= ((a & 0b01111111) << (7 * numRead));
        numRead++;
        if (numRead > 5)
        {
            throw new Exception("VarInt is too big");
        }

        if ((a & 0b10000000) == 0)
        {
            finished = true;
        }

        return finished;
    }

    public static int readVarint(IEnumerable<byte> data, ref int ptr)
    {
        if (data == null || data.Count() == 0 || data.Count() <= ptr)
            throw new Exception("Invalid data");

        int numRead = 0;
        int result = 0;
        byte read;
        do
        {
            read = data.ElementAt(ptr);
            result |= ((read & 0b01111111) << (7 * numRead));

            numRead++;
            ptr++;

            if (numRead > 5)
            {
                throw new Exception("VarInt is too big");
            }
        } while ((read & 0b10000000) != 0);

        return result;
    }
    public static byte[] GetData(int s) => GetData((uint)s);
    public static byte[] GetData(uint s)
    {
        uint value = s;

        byte[] temp = new byte[5];

        byte numRead = 0;

        do
        {
            if (numRead >= 5)
                throw new Exception("VarInt is too big");

            if (numRead > 0)
                temp[numRead - 1] |= 0b10000000;

            temp[numRead] = (byte)(value & 0b01111111);
            value >>= 7;
            numRead++;

        } while (value != 0);

        if (numRead == 5)
            return temp;

        byte[] res = new byte[numRead];

        for (int i = 0; i < numRead; i++)
        {
            res[i] = temp[i];
        }

        return res;
    }
}