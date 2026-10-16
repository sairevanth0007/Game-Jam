using System;
using System.Collections.Generic;
using System.Text;

namespace LumenWave.MA
{
    /// <summary>
    /// Minimal hand-written OSC 1.0 encoder/decoder (strings, int32, float32).
    /// Layout: address (null-terminated, padded to 4 bytes), type tags ",sif..." (padded), then arguments
    /// (big-endian int32/float32, padded strings).
    /// </summary>
    public static class Osc
    {
        public static byte[] Encode(string address, params object[] args)
        {
            if (string.IsNullOrEmpty(address) || address[0] != '/') throw new ArgumentException("OSC address must start with '/'");
            var buf = new List<byte>(64);
            WriteString(buf, address);
            var tags = new StringBuilder(",");
            foreach (var a in args)
            {
                switch (a)
                {
                    case string _: tags.Append('s'); break;
                    case int _: tags.Append('i'); break;
                    case float _: tags.Append('f'); break;
                    default: throw new ArgumentException("Unsupported OSC arg type " + a?.GetType());
                }
            }
            WriteString(buf, tags.ToString());
            foreach (var a in args)
            {
                switch (a)
                {
                    case string s: WriteString(buf, s); break;
                    case int i: WriteInt(buf, i); break;
                    case float f: WriteInt(buf, BitConverter.ToInt32(BitConverter.GetBytes(f), 0)); break;
                }
            }
            return buf.ToArray();
        }

        static void WriteString(List<byte> buf, string s)
        {
            var bytes = Encoding.ASCII.GetBytes(s);
            buf.AddRange(bytes);
            int pad = 4 - (bytes.Length % 4);   // always at least one null terminator
            for (int i = 0; i < pad; i++) buf.Add(0);
        }

        static void WriteInt(List<byte> buf, int v)
        {
            buf.Add((byte)(v >> 24)); buf.Add((byte)(v >> 16)); buf.Add((byte)(v >> 8)); buf.Add((byte)v);
        }

        /// <summary>Decodes a message (used by tests and the debug listener).</summary>
        public static bool TryDecode(byte[] data, out string address, out List<object> args)
        {
            address = null; args = new List<object>();
            try
            {
                int p = 0;
                address = ReadString(data, ref p);
                var tags = ReadString(data, ref p);
                if (!tags.StartsWith(",")) return false;
                for (int t = 1; t < tags.Length; t++)
                {
                    switch (tags[t])
                    {
                        case 's': args.Add(ReadString(data, ref p)); break;
                        case 'i': args.Add(ReadInt(data, ref p)); break;
                        case 'f': args.Add(BitConverter.ToSingle(BitConverter.GetBytes(ReadInt(data, ref p)), 0)); break;
                        default: return false;
                    }
                }
                return true;
            }
            catch { return false; }
        }

        static string ReadString(byte[] d, ref int p)
        {
            int start = p;
            while (d[p] != 0) p++;
            var s = Encoding.ASCII.GetString(d, start, p - start);
            p += 4 - ((p - start) % 4);
            return s;
        }

        static int ReadInt(byte[] d, ref int p)
        {
            int v = (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];
            p += 4;
            return v;
        }
    }
}
