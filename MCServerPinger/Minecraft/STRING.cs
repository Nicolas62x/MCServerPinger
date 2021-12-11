
using System.Text;

struct STRING
{

    public static string readString(IEnumerable<byte> data, int ptr) => readString(data, ref ptr);
    public static string readString(IEnumerable<byte> data, ref int ptr)
    {
        int lenght = VARINT.readVarint(data, ref ptr);

        if (lenght > 32767 || lenght + ptr > data.Count())
            throw new Exception("String is too big " + lenght);

        byte[] tmp = new byte[lenght];

        for (int i = 0; i < lenght; i++)
        {
            tmp[i] = data.ElementAt(i + ptr);
        }

        ptr += lenght;

        return Encoding.UTF8.GetString(tmp);

    }

    public static byte[] GetData(string s)
    {
        byte[] dat = Encoding.UTF8.GetBytes(s);

        if (dat.Length > 32767)
            throw new Exception("String is too big " + dat.Length);

        byte[] l = VARINT.GetData(dat.Length);

        return l.Concat(dat).ToArray();
    }
}