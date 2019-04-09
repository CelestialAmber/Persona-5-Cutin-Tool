using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace P5CutinTool
{
    class Program
    {

        static string GLH_MAGIC = "0HLG";
        static string GLZ_MAGIC = "0ZLG";

        static byte[] glz_dec(byte[] data)
        {
            string glz_magic = Encoding.UTF8.GetString(data,0,4);
            if(glz_magic != GLZ_MAGIC)
            {
                Console.WriteLine("Invalid GLZ data.");
                return data;
            }
            int unk = (int)BitConverter.ToUInt32(data,0x04);
            int dec_size = (int)BitConverter.ToUInt32(data,0x08);
            int cmp_size = (int)BitConverter.ToUInt32(data,0x0C);
            byte marker = data[0x10];
            List<byte> res = new List<byte>();
            int p = 32;
            while(p < data.Length)
            {
                byte b = data[p];
                p += 1;
                if(b == marker)
                {
                    byte offset = data[p];
                    p += 1;

                    if(offset == marker)
                    {
                       res.Add(marker);
                        continue;
                    }
                    byte count = data[p];
                    p += 1;

                    if (offset > marker) offset -= 1;
                    for(int i = 0; i < count; i++)
                    {
                        res.Add(res[res.Count - offset]);
                    }
                }
                else
                {
                    res.Add(b);
                }

            }
            return res.ToArray();
        }
        static byte[] glh_unpack(byte[] data)
        {
            string glh_magic = Encoding.UTF8.GetString(data, 0, 4);
            if(glh_magic != GLH_MAGIC)
            {
                Console.WriteLine("Invalid GLH data.");
                return data;
            }
            int unk1 = (int)BitConverter.ToUInt32(data, 0x04);
            int unk2 = (int)BitConverter.ToUInt32(data, 0x08);
            int dec_size = (int)BitConverter.ToUInt32(data, 0x0C);
            int cmp_size = (int)BitConverter.ToUInt32(data, 0x10);
            byte[] glh_data = data.Skip(0x20).ToArray();
            string magic = Encoding.UTF8.GetString(glh_data, 0, 4);
            if (magic == GLZ_MAGIC) return glz_dec(glh_data);
            else return glh_data;

        }
         static void unpackCutin(string filename)
        {
            FileInfo file = new FileInfo(filename);
            long length = file.Length;
            byte[] bytes = File.ReadAllBytes(filename);
            BinaryReader binReader = new BinaryReader(File.OpenRead(filename));
            int item_count = (int)BitConverter.ToInt32(binReader.ReadBytes(4).Reverse().ToArray(), 0);
            for (int i = 0; i < item_count; i++)
            {
                if(binReader.BaseStream.Position >= length - 8)
                {
                    return;
                }
                int item_id = (int)BitConverter.ToInt32(binReader.ReadBytes(4).Reverse().ToArray(),0);
                int item_size = (int)BitConverter.ToInt32(binReader.ReadBytes(4).Reverse().ToArray(), 0);
                byte[] item_data = binReader.ReadBytes(item_size);
                item_data = glh_unpack(item_data);
                string outName = filename.Substring(filename.LastIndexOf(@"\") + 1);
                string out_path = filename.Replace(outName,"");
                out_path = out_path.Substring(0,out_path.Length - 1) + "_extracted/";
                outName += "-" + i + ".dds";
                if(!Directory.Exists(out_path)) Directory.CreateDirectory(out_path);
                Console.WriteLine("Saving " + outName);
                File.WriteAllBytes(out_path + outName, item_data);

            }

        }
        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine("Usage: P5CutinTool cutinDir");
                return;
            }
            else if(args.Length > 1)
            {
                Console.WriteLine("Invalid number of arguments.");
                return;
            }
            else
            {
                string dir = args[0];
                string[] files;
                try
                {
                    files = Directory.EnumerateFiles(dir,"*.*",SearchOption.AllDirectories).Where(x => x.Substring(x.LastIndexOf(".")+1) == "000" || x.Substring(x.LastIndexOf(".") + 1) == "001").ToArray();
                    
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine("Directory not found.");
                    return;
                }
                foreach (string file in files)
                {

                    Console.WriteLine("Extracting " + file.Substring(file.LastIndexOf(@"\") + 1));
                    unpackCutin(file);
                }
                
               
            }
        }
    }
}
