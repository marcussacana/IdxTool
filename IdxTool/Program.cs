using AdvancedBinary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IdxTool {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("IdxTool - By Marcussacana");
            if (args == null || args.Length == 0)
                Console.WriteLine("Usage:\nIdxTool -extract %file% -repack %dir%");
            bool Repack = false;
            foreach (string arg in args) {
                string flag = arg.Trim(new char[] { ' ', '-', '\\' }).ToLower();
                if (flag == "repack")
                    Repack = true;
                else if (flag == "extract")
                    Repack = false;
                if (Repack) {
                    if (Directory.Exists(arg)) {
                        RepackDir(arg);
                    }
                } else {
                    if (File.Exists(arg)) {
                        Extract(arg);
                    }
                }
            }
            Console.ReadKey();
        }

        private static void RepackDir(string arg) {
            Console.WriteLine("Initializing Variables...");
            if (arg.EndsWith("\\"))
                arg = arg.Substring(0, arg.Length - 1);
            string IDXPath = arg + ".IDX";
            string BINPath = arg + ".BIN";
            string[] Files = GetFiles(arg, "*.bin|*.zlib|*.str");
            long[] DecompressedSizes = new long[Files.Length];
            TextReader Info = File.OpenText(arg + "\\Packget Info.txt");
            PerareFiles(ref Files,);
            StructWriter IDX = new StructWriter(new StreamWriter(IDXPath).BaseStream);
            Stream BIN = new StreamWriter(BINPath).BaseStream;
            Console.WriteLine("Initialized...");
            foreach (string File in Files) {
                IDXEntry Entry = new IDXEntry() {
                    Offset = BIN.Position,
                    CompressedSize = new FileInfo(File).Length,
                    DecompressedSize = File.ToLower().EndsWith(".zlib") ? long.Parse(Info.ReadLine()) : new FileInfo(File).Length,
                    IsCompressed = File.ToLower().EndsWith(".zlib")
                };
                Console.WriteLine("Off: {0:X8}|Len: {1:X8}|DLen: {2:X8}|IsComp: {3:X8}", Entry.Offset, Entry.CompressedSize, Entry.DecompressedSize, Entry.IsCompressed);
                Stream Reader = new StreamReader(File).BaseStream;
                int Readed = 0;
                byte[] Buffer = new byte[1024];
                do {
                    Readed = Reader.Read(Buffer, 0, Buffer.Length);
                    BIN.Write(Buffer, 0, Readed);
                } while (Readed > 0);
                Reader.Close();
                IDX.WriteStruct(ref Entry);
            }
            IDX.Close();
            BIN.Close();
            Info.Close();
            Console.WriteLine("Packget Repacked.");
        }
        
        private static string[] GetFiles(string Dir, string Search) {
            string[] Result = new string[0];
            foreach (string pattern in Search.Split('|'))
                Result = Result.Union(Directory.GetFiles(Dir, pattern)).ToArray();
            return Result;
        }
        private static void PerareFiles(ref string[] files) {
            int[] Keys = new int[files.Length];
            for (int i = 0; i < files.Length; i++) {
                string FN = Path.GetFileNameWithoutExtension(files[i]);
                try {
                    Keys[i] = Convert.ToInt32(FN, 16);
                }
                catch {
                    Console.WriteLine(files[i] + " have a invalid file name, Sorry.");
                    Console.ReadKey();
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
            }
            Array.Sort(Keys, files);
        }

        private static void Extract(string arg) {
            string IDXPath;
            string BINPath;
            string OutDir;
            DetectPath(out IDXPath, out BINPath, out OutDir, arg);
            if (!Directory.Exists(OutDir))
                Directory.CreateDirectory(OutDir);
            TextWriter Info = File.CreateText(OutDir + "\\Packget Info.txt");
            StructReader IDX = new StructReader(new StreamReader(IDXPath).BaseStream);
            Stream BIN = new StreamReader(BINPath).BaseStream;
            long Count = IDX.BaseStream.Length / Tools.GetStructLength(new IDXEntry());
            Console.WriteLine("{0} and {1} Open, {2} Files...\nStarting Extraction...", Path.GetFileName(IDXPath), Path.GetFileName(BINPath), Count);
            for (long i = 0; i < Count; i++) {
                IDXEntry Entry = new IDXEntry();
                IDX.ReadStruct(ref Entry);
                BIN.Position = Entry.Offset;
                string FN = i.ToString("X8");
                if (Entry.IsCompressed)
                    FN += ".zlib";
                else if (IsString(BIN, Entry.CompressedSize))
                    FN += ".str";
                else
                    FN += ".bin";
                if (Entry.IsCompressed)
                    Info.WriteLine(Entry.DecompressedSize);
                Console.WriteLine("Off: {1:X9}|Len: {2:X8}|DLen: {3:X8}|IsComp: {4}|{0}", FN, Entry.Offset, Entry.CompressedSize, Entry.DecompressedSize, Entry.IsCompressed ? "True " : "False");
                if (File.Exists(OutDir + FN))
                    File.Delete(OutDir + FN);
                Stream Writer = new StreamWriter(OutDir + FN).BaseStream;
                long Readed = 0;
                byte[] Buffer = new byte[1024];
                if (Entry.CompressedSize > 0)
                    do {
                        if (Readed + Buffer.Length >= Entry.CompressedSize)
                            Buffer = new byte[Entry.CompressedSize - Readed];
                        if (BIN.Read(Buffer, 0, Buffer.Length) == 0)
                            throw new Exception("Failed to Read");
                        Readed += Buffer.Length;
                        Writer.Write(Buffer, 0, Buffer.Length);
                    } while (Readed < Entry.CompressedSize);
                Writer.Close();
            }
            IDX.Close();
            BIN.Close();
            Info.Close();
            Console.WriteLine("Packget Extracted.");
        }

        private static bool IsString(Stream BIN, long Length) {
            long Pointer = BIN.Position;
            if (Length < 0x18)
                return false;
            try {
                BinaryReader Reader = new BinaryReader(BIN);
                Reader.ReadUInt32();
                Reader.BaseStream.Seek(Reader.ReadUInt32(), SeekOrigin.Current);
                Reader.ReadUInt32();
                int v1 = Reader.ReadInt32();
                int v2 = Reader.ReadInt32();
                BIN.Position = Pointer;
                if (v1 == 0x14 && v2 == 0x00)
                    return true;
                else
                    return false;
            }catch {
                BIN.Position = Pointer;
                return false;
            }
        }

        struct IDXEntry {
            internal long Offset;
            internal long DecompressedSize;
            internal long CompressedSize;
            private long _IsCompressed;

            [Ignore]
            internal bool IsCompressed { get { return _IsCompressed > 0; } set { _IsCompressed = value ? 1 : 0; } }
        }

        private static void DetectPath(out string IDX, out string BIN, out string DIR, string Arg) {
            if (Arg.ToLower().EndsWith(".idx")) {
                IDX = Arg;
                BIN = IDX.Substring(0, IDX.Length - 3) + "bin";
            } else if (Arg.ToLower().EndsWith(".bin")) {
                BIN = Arg;
                IDX = BIN.Substring(0, BIN.Length - 3) + "idx";
            } else if (System.IO.File.Exists(Arg + ".bin") && System.IO.File.Exists(Arg + ".idx")) {
                IDX = Arg + ".idx";
                BIN = Arg + ".bin";
            } else if (System.IO.File.Exists(Arg + "bin") && System.IO.File.Exists(Arg + "idx")) {
                IDX = Arg + "idx";
                BIN = Arg + "bin";
            } else
                throw new Exception("Invalid Input");
            DIR = IDX.Substring(0, IDX.Length - 4) + "~\\";
        }
    }
}
