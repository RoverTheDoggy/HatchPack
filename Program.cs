using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using zlib;

namespace HatchPack {
    class Program {
        static List<string> compressMatches = new List<string> {
            "*.txt", "*.json", "*.xml", "*.tmx", "*.glsl",
            "*.wav",
            "*.obj", "*.mtl",
            "*.fbx", "*.dae", "*.hmdl",
            "*.ttf"
        };

        static List<string> encryptMatches = new List<string> {
            "*.ibc", "*.hcm",
            "*.hscn"
        };

        static List<string> nameMatches = new List<string>();

        static List<string> excludeMatches = new List<string>();

        static bool Opt_Compress(List<string> args) {
            if (args.Count == 0) {
                return true;
            }

            compressMatches.Clear();

            foreach (string arg in args) {
                compressMatches.Add(arg);
            }

            return false;
        }

        static bool Opt_Encrypt(List<string> args) {
            if (args.Count == 0) {
                return true;
            }

            encryptMatches.Clear();

            foreach (string arg in args) {
                encryptMatches.Add(arg);
            }

            return false;
        }

        static bool Opt_Pack(List<string> args) {
            if (args.Count == 0) {
                return true;
            }

            nameMatches.Clear();

            foreach (string arg in args) {
                nameMatches.Add(arg);
            }

            return false;
        }

        static bool Opt_Exclude(List<string> args) {
            if (args.Count == 0) {
                return true;
            }

            excludeMatches.Clear();

            foreach (string arg in args) {
                excludeMatches.Add(arg);
            }

            return false;
        }

        static bool ParseOption(string option, List<string> args) {
            switch (option) {
                case "--compress":
                case "-c":
                    Opt_Compress(args);
                    return true;
                case "--encrypt":
                case "-e":
                    Opt_Encrypt(args);
                    return true;
                case "--pack":
                    Opt_Pack(args);
                    return true;
                case "--exclude":
                case "-x":
                    Opt_Exclude(args);
                    return true;
                default:
                    Console.WriteLine("Unrecognized option " + option);
                    return false;
            }
        }

        static bool ParseCommandLineArgs(List<string> args) {
            for (int i = 0; i < args.Count;) {
                if (args[i] == "--") {
                    args.RemoveAt(i);
                    return true;
                }
                else if (args[i].StartsWith("-") || args[i].StartsWith("--")) {
                    string option = args[i];

                    List<string> optionArgs = new List<string>();

                    args.RemoveAt(i);

                    while (i < args.Count) {
                        if (args[i].StartsWith("-")) {
                            break;
                        }

                        optionArgs.Add(args[i]);
                        args.RemoveAt(i);
                    }

                    if (!ParseOption(option, optionArgs)) {
                        return false;
                    }
                }
                else {
                    i++;
                }
            }

            return true;
        }

        static UInt32 CRC32_EncryptString(string message) {
            int i, j;
            UInt32 bytee, crc, mask;

            i = 0;
            crc = 0xFFFFFFFF;
            while (i < message.Length) {
                bytee = message[i];
                crc = crc ^ bytee;
                for (j = 7; j >= 0; j--) {
                    mask = 0xFFFFFFFF * (crc & 1);
                    crc = (crc >> 1) ^ (0xEDB88320 & mask);
                }
                i++;
            }
            return ~crc;
        }
        static UInt32 CRC32_EncryptData(byte[] data) {
            int i, j;
            UInt32 bytee, crc, mask;

            i = 0;
            crc = 0xFFFFFFFF;
            while (i < data.Length) {
                bytee = data[i];
                crc = crc ^ bytee;
                for (j = 7; j >= 0; j--) {
                    mask = 0xFFFFFFFF * (crc & 1);
                    crc = (crc >> 1) ^ (0xEDB88320 & mask);
                }
                i++;
            }
            return ~crc;
        }
        static void CryptoXOR(ref byte[] data, string filename, bool dec) {
            byte[] keyA = new byte[16];
            byte[] keyB = new byte[16];
            UInt64 fileSize = (UInt64)data.Length;
            UInt32 filenameHash = CRC32_EncryptString(filename);
            UInt32 sizeHash = CRC32_EncryptData(BitConverter.GetBytes(fileSize));

            byte[] filenameHashBytes = BitConverter.GetBytes(filenameHash);
            byte[] sizeHashBytes = BitConverter.GetBytes(sizeHash);

            // Populate Key A
            for (var i = 0; i < 16; i++) {
                keyA[i] = filenameHashBytes[i & 3];
            }

            // Populate Key B
            for (var i = 0; i < 16; i++) {
                keyB[i] = sizeHashBytes[i & 3];
            }

            bool swapNibbles = false;
            int indexKeyA = 0;
            int indexKeyB = 8;
            int xorValue = (int)((fileSize / 4) & 0x7F);
            for (uint x = 0; x < fileSize; x++) {
                int temp = data[x];

                if (dec)
                    temp ^= xorValue ^ keyB[indexKeyB++];
                else
                    temp ^= keyA[indexKeyA++];

                if (swapNibbles)
                    temp = (((temp & 0x0F) << 4) | ((temp & 0xF0) >> 4));

                if (!dec)
                    temp ^= xorValue ^ keyB[indexKeyB++];
                else
                    temp ^= keyA[indexKeyA++];

                data[x] = (byte)temp;

                if (indexKeyA <= 15) {
                    if (indexKeyB > 12) {
                        indexKeyB = 0;
                        swapNibbles = !swapNibbles;
                    }
                }
                else if (indexKeyB <= 8) {
                    indexKeyA = 0;
                    swapNibbles = !swapNibbles;
                }
                else {
                    xorValue = (xorValue + 2) & 0x7F;
                    if (swapNibbles) {
                        swapNibbles = false;
                        indexKeyA = xorValue % 7;
                        indexKeyB = (xorValue % 12) + 2;
                    }
                    else {
                        swapNibbles = true;
                        indexKeyA = (xorValue % 12) + 3;
                        indexKeyB = xorValue % 7;
                    }
                }
            }
        }

        static string GetFilesizeString(UInt64 size) {
            float sizeDecimal = size;

            if (size >= 1024 * 1024 * 1024) {
                sizeDecimal /= 1024 * 1024 * 1024;
                return $"{sizeDecimal:F2} GiB";
            }
            else if (size >= 1024 * 1024) {
                sizeDecimal /= 1024 * 1024;
                return $"{sizeDecimal:F2} MiB";
            }
            else if (size >= 1024) {
                sizeDecimal /= 1024;
                return $"{sizeDecimal:F2} KiB";
            }

            return size + " bytes";
        }

        static List<string> GetFileList(string resourcesFolder) {
            if (nameMatches.Count == 0) {
                string[] filePaths = Directory.GetFiles(resourcesFolder, "*.*", SearchOption.AllDirectories);
                return new List<string>(filePaths);
            }

            List<string> filesToPack = new List<string>();

            foreach (string pattern in nameMatches) {
                string[] filePaths = Directory.GetFiles(resourcesFolder, pattern, SearchOption.AllDirectories);
                foreach (string file in filePaths) {
                    string realPath = file.Substring(resourcesFolder.Length).Replace('\\', '/');
                    if (excludeMatches.Any(match => realPath.WildcardMatch(match))) {
                        Console.WriteLine("Excluding file " + realPath);
                        continue;
                    }

                    filesToPack.Add(file);
                }
            }

            return filesToPack;
        }

        static bool PackHatchFile(string outFilename, string resourcesFolder) {
            UInt64 offsetGLOB = 0;
            using (FileStream stream = new FileStream(outFilename, FileMode.Create)) {
                stream.Write(new byte[] { 0x48, 0x41, 0x54, 0x43, 0x48 }, 0, 5); // HATCH
                stream.Write(new byte[] { 0x01, 0x00, 0x00 }, 0, 3); // 1.0.0

                List<string> filesToPack = GetFileList(resourcesFolder);

                if (filesToPack.Count > 65535) {
                    Console.WriteLine("Too many files to pack! (Count is " + filesToPack.Count + ", maximum is 65535)");
                    return false;
                }

                stream.WriteByte((byte)(filesToPack.Count & 0xFF));
                stream.WriteByte((byte)(filesToPack.Count >> 8 & 0xFF));

                UInt64 tocEnd = (UInt64)(stream.Position + 32 * filesToPack.Count);

                UInt64 compressedTotal = 0;
                UInt64 uncompressedTotal = 0;

                foreach (string file in filesToPack) {
                    string realPath = file.Substring(resourcesFolder.Length).Replace('\\', '/');
                    UInt32 hash = CRC32_EncryptString(realPath);
                    UInt64 offset = tocEnd + offsetGLOB;

                    byte[] fileBytes = File.ReadAllBytes(file);

                    bool needsCompression = compressMatches.Any(match => realPath.WildcardMatch(match));
                    bool needsEncryption = encryptMatches.Any(match => realPath.WildcardMatch(match));

                    UInt64 size = (UInt64)fileBytes.Length;
                    UInt32 dataType = 0;
                    UInt64 compressedSize = size;

                    // Print what file is going to be packed before compressing or encrypting it
                    Console.Write(hash.ToString("X8") + ": " + realPath + " ");

                    if (needsCompression || needsEncryption) {
                        Console.Write("...");
                    }

                    if (needsCompression) {
                        using (MemoryStream outMemoryStream = new MemoryStream())
                        using (ZOutputStream compress = new ZOutputStream(outMemoryStream, zlibConst.Z_BEST_COMPRESSION)) {
                            compress.Write(fileBytes, 0, fileBytes.Length);
                            compress.finish();

                            fileBytes = outMemoryStream.ToArray();
                            compressedSize = (UInt64)fileBytes.Length;
                        }
                    }

                    if (needsEncryption) {
                        CryptoXOR(ref fileBytes, realPath, false);

                        dataType = 2;
                    }

                    // Erase the ellipses
                    if (needsCompression || needsEncryption) {
                        Console.Write("\b\b\b");
                    }

                    Console.Write("(Size: " + GetFilesizeString(size));
                    if (needsCompression) {
                        Console.Write(", Compressed: " + GetFilesizeString(compressedSize));
                    }
                    if (needsEncryption) {
                        Console.Write(", Encrypted");
                    }
                    Console.WriteLine(")");

                    stream.Write(BitConverter.GetBytes(hash), 0, 4);
                    stream.Write(BitConverter.GetBytes(offset), 0, 8);
                    stream.Write(BitConverter.GetBytes(size), 0, 8);
                    stream.Write(BitConverter.GetBytes(dataType), 0, 4);
                    stream.Write(BitConverter.GetBytes(compressedSize), 0, 8);

                    Int64 mark = stream.Position;

                    stream.Seek((Int64)offset, SeekOrigin.Begin);
                    stream.Write(fileBytes, 0, (int)compressedSize);

                    stream.Seek(mark, SeekOrigin.Begin);

                    offsetGLOB += compressedSize;

                    uncompressedTotal += size;
                    compressedTotal += compressedSize;
                }

                Console.WriteLine("Packed " + filesToPack.Count + " files");

                if (compressedTotal < uncompressedTotal) {
                    Console.WriteLine("Compressed " + GetFilesizeString(uncompressedTotal) + " to " + GetFilesizeString(compressedTotal));
                }
            }

            return true;
        }

        [DllImport("kernel32.dll")]
        static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

        // Attempt to detect whether the program was launched from a terminal or a double click
        // From https://devblogs.microsoft.com/oldnewthing/20160125-00/?p=92922
        static bool WasLaunchedFromTerminal() {
            uint[] processList = new uint[1];
            return GetConsoleProcessList(processList, 1) > 1;
        }

        static bool OpenResourcesFolderFileDialog(out string resourcesFolder) {
            resourcesFolder = "";

            using (CommonOpenFileDialog commonOpenFileDialog = new CommonOpenFileDialog()) {
                commonOpenFileDialog.IsFolderPicker = true;
                if (Properties.Settings1.Default.LastOpen != "")
                    commonOpenFileDialog.InitialDirectory = Properties.Settings1.Default.LastOpen;

                if (commonOpenFileDialog.ShowDialog() == CommonFileDialogResult.Ok &&
                    !string.IsNullOrWhiteSpace(commonOpenFileDialog.FileName) &&
                    commonOpenFileDialog.FileName.Contains("Resources")) {
                    resourcesFolder = commonOpenFileDialog.FileName;

                    Properties.Settings1.Default.LastOpen = resourcesFolder;
                    Properties.Settings1.Default.Save();

                    return true;
                }
            }

            return false;
        }

        static bool OpenOutputFileDialog(string resourcesFolder, out string outFilename) {
            outFilename = "";

            using (SaveFileDialog saveFileDialog = new SaveFileDialog()) {
                saveFileDialog.InitialDirectory = resourcesFolder;
                if (Properties.Settings1.Default.LastSave != "")
                    saveFileDialog.InitialDirectory = Properties.Settings1.Default.LastSave;

                saveFileDialog.FileName = "Data.hatch";
                saveFileDialog.Filter = "Hatch Data Pack (*.hatch)|*.hatch";
                saveFileDialog.FilterIndex = 2;
                saveFileDialog.RestoreDirectory = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK) {
                    outFilename = saveFileDialog.FileName;

                    Properties.Settings1.Default.LastSave = Directory.GetParent(outFilename).FullName;
                    Properties.Settings1.Default.Save();

                    return true;
                }
            }

            return false;
        }

        static void PrintUsage() {
            Console.Write("usage: hatchpack ");
            Console.Write("[--compress | -c file...] ");
            Console.Write("[--encrypt | -e file...] ");
            Console.Write("[--pack file...] ");
            Console.Write("[--exclude | -x file...] ");
            Console.Write("[-h | --help] ");
            Console.Write("input_dir output_file");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Positional arguments:");
            Console.WriteLine("  input_dir        The path to a directory containing the resources to pack.");
            Console.WriteLine("  output_file      The path to the output file.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --compress, -c   A list of files to compress. Supports wildcards.");
            Console.WriteLine("                   Default: " + string.Join(", ", compressMatches));
            Console.WriteLine("  --encrypt, -e    A list of files to encrypt. Supports wildcards.");
            Console.WriteLine("                   Default: " + string.Join(", ", encryptMatches));
            Console.WriteLine("  --pack           A list of files to pack. Supports wildcards.");
            Console.WriteLine("                   By default, all files in the input directory are packed.");
            Console.WriteLine("  --exclude, -x    A list of files to exclude from packing. Supports wildcards.");
            Console.WriteLine("  -h, --help       Show this message and exit.");
        }

        [STAThread]
        static int Main(string[] args) {
            if (args.Length == 0 || args.Any(match => match == "-h" || match == "--help")) {
                PrintUsage();
                return 1;
            }

            string outFilename = "", resourcesFolder = "";

            List<string> cmdLineArgs = new List<string>(args);

            if (!ParseCommandLineArgs(cmdLineArgs)) {
                return 1;
            }

            if (cmdLineArgs.Count >= 1 && !cmdLineArgs[0].StartsWith("-"))
                resourcesFolder = cmdLineArgs[0];
            if (cmdLineArgs.Count >= 2 && !cmdLineArgs[1].StartsWith("-"))
                outFilename = cmdLineArgs[1];

            if (!WasLaunchedFromTerminal()) {
                if (resourcesFolder == "") {
                    OpenResourcesFolderFileDialog(out resourcesFolder);
                }
                if (resourcesFolder != "" && outFilename == "") {
                    OpenOutputFileDialog(resourcesFolder, out outFilename);
                }
            }

            if (resourcesFolder == "" || outFilename == "") {
                Console.WriteLine("Missing argument");
                PrintUsage();
                return 1;
            }

            resourcesFolder = resourcesFolder.Replace('\\', '/');
            if (resourcesFolder[resourcesFolder.Length - 1] != '/')
                resourcesFolder += "/";

            if (!PackHatchFile(outFilename, resourcesFolder)) {
                return 1;
            }

            Console.WriteLine("Done!");

            return 0;
        }
    }

    public static class StringExtensions {
        public static bool WildcardMatch(this string text, string pattern) {
            Regex regex = new("^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return regex.IsMatch(text);
        }
    }
}
