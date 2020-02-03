using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace P5CutinTool
{
    class Program
    {
        static string GLH_MAGIC = "0HLG";
        static string GLZ_MAGIC = "0ZLG";

        static int Read_i32_be(BinaryReader r)
        {
            return BitConverter.ToInt32(r.ReadBytes(4).Reverse().ToArray(), 0);
        }
        static void Write_i32_be(BinaryWriter w, int v)
        {
            w.Write(BitConverter.GetBytes(v).Reverse().ToArray());
        }
        static void Write_i32(byte[] b, int off, int v)
        {
            var tmp = BitConverter.GetBytes(v);
            Buffer.BlockCopy(tmp, 0, b, off, tmp.Length);
        }

        static byte[] glz_dec(byte[] data)
        {
            if (Encoding.ASCII.GetString(data, 0, 4) != GLZ_MAGIC)
            {
                Console.WriteLine("Invalid GLZ data.");
                return data;
            }

            //uint unk = BitConverter.ToUInt32(data, 4);
            //uint dec_size = BitConverter.ToUInt32(data, 8);
            //uint cmp_size = BitConverter.ToUInt32(data, 12);

            byte marker = data[16];
            var res = new List<byte>();

            for (int p = 32; p < data.Length;)
            {
                byte b = data[p++];
                if (b != marker)
                {
                    res.Add(b);
                    continue;
                }

                byte offset = data[p++];
                if (offset == marker)
                {
                    res.Add(marker);
                    continue;
                }
                if (offset > marker)
                    --offset;

                byte count = data[p++];
                for (int i = 0; i < count; ++i)
                {
                    res.Add(res[res.Count - offset]);
                }
            }
            return res.ToArray();
        }

        static byte[] glz_compress(byte[] data)
        {
            var compressed = new List<int>();
            var freq = Enumerable.Range(0, 0x100).ToDictionary(x => x, x => 0);

            for (int i = 0; i < data.Length;)
            {
                int o_min = 0, len_max = 0;
                for (int o = Math.Min(0xfe, i); o > 0; --o)
                {
                    int len = 0;
                    while (len < Math.Min(data.Length - i, o) && data[i - o + len] == data[i + len])
                        ++len;
                    if (len >= len_max)
                    {
                        o_min = o;
                        len_max = len;
                    }
                }
                if (len_max > 3)
                {
                    i += len_max;
                    compressed.AddRange(new int[] { -1, o_min, len_max });
                }
                else
                {
                    var v = data[i++];
                    compressed.Add(v);
                    ++freq[v];
                }
            }

            byte marker = (byte)freq.OrderBy(x => x.Value).First().Key;

            var compressed2 = new byte[32].ToList();
            for (int i = 0; i < compressed.Count; ++i)
            {
                if (compressed[i] == -1)
                {
                    var off = compressed[i + 1];
                    var size = compressed[i + 2];
                    if (off >= marker)
                        ++off;

                    compressed2.Add(marker);
                    compressed2.Add((byte)off);
                    compressed2.Add((byte)size);
                    i += 2;
                    continue;
                }

                var v = (byte)compressed[i];
                compressed2.Add(v);
                if (v == marker)
                    compressed2.Add(v);
            }

            var compressed3 = compressed2.ToArray();
            var magic = Encoding.ASCII.GetBytes(GLZ_MAGIC);
            Buffer.BlockCopy(magic, 0, compressed3, 0, magic.Length);
            Write_i32(compressed3, 4, 0x01105030);
            Write_i32(compressed3, 8, data.Length);
            Write_i32(compressed3, 12, compressed3.Length);
            compressed3[16] = marker;

            return compressed3;
        }

        static byte[] glh_unpack(byte[] data)
        {
            if (Encoding.ASCII.GetString(data, 0, 4) != GLH_MAGIC)
            {
                Console.WriteLine("Invalid GLH data.");
                return data;
            }

            //uint unk1 = BitConverter.ToUInt32(data, 4);
            //uint unk2 = BitConverter.ToUInt32(data, 8);
            //uint dec_size = BitConverter.ToUInt32(data, 12);
            //uint cmp_size = BitConverter.ToUInt32(data, 16);

            return glz_dec(data.Skip(0x20).ToArray());
        }

        static byte[] glh_pack(byte[] data)
        {
            var glz = glz_compress(data);
            var glh = Enumerable.Repeat<Byte>(0, 0x20).Concat(glz).ToArray();
            var magic = Encoding.ASCII.GetBytes(GLH_MAGIC);
            Buffer.BlockCopy(magic, 0, glh, 0, magic.Length);
            Write_i32(glh, 4, 0x01105030);
            Write_i32(glh, 8, 0x00000001);
            Write_i32(glh, 12, data.Length);
            Write_i32(glh, 16, glz.Length + 0x20);

            return glh;
        }

        static void UnpackCutin(string filename, string outDir)
        {
            long file_len = new FileInfo(filename).Length;
            var r = new BinaryReader(File.OpenRead(filename));
            int item_count = Read_i32_be(r);

            for (int i = 0; i < item_count; i++)
            {
                if (r.BaseStream.Position >= file_len - 8)
                    return;

                int item_id = Read_i32_be(r);
                int item_size = Read_i32_be(r);
                var item_data = glh_unpack(r.ReadBytes(item_size));

                string outFile = Path.Combine(outDir, Path.GetFileName(filename) + "-" + item_id + ".dds");
                File.WriteAllBytes(outFile, item_data);
            }
        }

        static void MakeCutin(string[] filenames, string outFile)
        {
            using (var w = new BinaryWriter(File.OpenWrite(outFile)))
            {
                Write_i32_be(w, filenames.Length);

                for (int i = 0; i < filenames.Length; ++i)
                {
                    var dds = File.ReadAllBytes(filenames[i]);
                    var packed = glh_pack(dds);
                    Write_i32_be(w, i);
                    Write_i32_be(w, packed.Length);
                    w.Write(packed);
                }
            }
        }

        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Usage: P5CutinTool <pack|unpack> <src dir> <dst dir>");
                return;
            }

            string cmd = args[0], src = args[1], dst = args[2];

            if (!Directory.Exists(src))
            {
                Console.WriteLine("Directory not found.");
                return;
            }

            if (!Directory.Exists(dst))
                Directory.CreateDirectory(dst);

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;

            switch (cmd)
            {
                case "unpack":
                    //foreach (var file in Directory.EnumerateFiles(src, "*.00?", SearchOption.AllDirectories))
                    Parallel.ForEach(Directory.EnumerateFiles(src, "*.00?", SearchOption.AllDirectories), file =>
                    {
                        Console.WriteLine("Extracting " + Path.GetFileName(file));
                        var outDir = Path.Combine(dst, Path.GetDirectoryName(Path.GetRelativePath(src, file)));
                        if (!Directory.Exists(outDir))
                            Directory.CreateDirectory(outDir);
                        UnpackCutin(file, outDir);
                    });
                    break;
                case "pack":
                    Parallel.ForEach(Directory.EnumerateFiles(src, "*.dds", SearchOption.AllDirectories).GroupBy(p =>
                    {
                        var f = Path.GetFileNameWithoutExtension(p); return f.Substring(0, f.LastIndexOf('-'));
                    }).Select(x => x.OrderBy(f => Regex.Replace(f, "[0-9]+", m => m.Value.PadLeft(8, '0')))), files =>
                    {
                        var fn = Path.GetFileNameWithoutExtension(files.First());
                        var fn_base = fn.Substring(0, fn.LastIndexOf('-'));
                        var outDir = Path.Combine(dst, Path.GetRelativePath(src, Path.GetDirectoryName(files.First())));
                        var outfile = Path.Combine(outDir, fn_base);
                        if (!Directory.Exists(outDir))
                            Directory.CreateDirectory(outDir);

                        Console.WriteLine("Packing " + Path.GetFileName(outfile));
                        MakeCutin(files.ToArray(), outfile);
                    });
                    break;
                default:
                    Console.WriteLine($"Unknown command \"{args[0]}\"");
                    return;
            }
        }
    }
}
