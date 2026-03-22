using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using zlib;

namespace HatchPack {
    class Program {
        static bool encryptAllFiles = true;

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

            // Console.WriteLine(filename + " filenameHash: " + filenameHash.ToString("X08") + " sizeHash: " + sizeHash.ToString("X08"));

            byte[] filenameHashBytes = BitConverter.GetBytes(filenameHash);
            byte[] sizeHashBytes = BitConverter.GetBytes(sizeHash);

            // Console.WriteLine();
            // Console.WriteLine();

            // Populate Key A
            // Console.Write("Key A: ");
            for (var i = 0; i < 16; i++) {
                keyA[i] = filenameHashBytes[i & 3];
                // Console.Write(keyA[i].ToString("X02") + " ");
            }

            // Populate Key B
            // Console.Write("Key B: ");
            for (var i = 0; i < 16; i++) {
                keyB[i] = sizeHashBytes[i & 3];
                // Console.Write(keyB[i].ToString("X02") + " ");
            }

            // Console.WriteLine();

            bool swapNibbles = false;
            int indexKeyA = 0;
            int indexKeyB = 8;
            int xorValue = (int)((fileSize / 4) & 0x7F);
            // Console.WriteLine("xorValue: " + xorValue.ToString("X02"));
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

                /*
                if (x < 8)
                    Console.Write("" + data[x].ToString("X02") + " -> " + temp.ToString("X02") + " ");
                if (x == 7)
                    Console.WriteLine();
                //*/
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

        static void PackHatch1(string out_filename, string resourcesFolder) {
            string resourcesIEDAT = out_filename;

            UInt64 offsetGLOB = 0;
            using (FileStream stream = new FileStream(resourcesIEDAT, FileMode.Create)) {
                stream.Write(new byte[] { 0x48, 0x41, 0x54, 0x43, 0x48 }, 0, 5); // HATCH
                stream.Write(new byte[] { 0x01, 0x00, 0x00 }, 0, 3); // 1.0.0

                string[] filePaths = Directory.GetFiles(resourcesFolder, "*.*", SearchOption.AllDirectories);

                stream.WriteByte((byte)(filePaths.Length & 0xFF));
                stream.WriteByte((byte)(filePaths.Length >> 8 & 0xFF));

                UInt64 tocEnd = (UInt64)(stream.Position + 32 * filePaths.Length);

                UInt64 compressedTotal = 0;
                UInt64 uncompressedTotal = 0;

                foreach (string file in filePaths) {
                    string realPath = file.Substring(resourcesFolder.Length).Replace('\\', '/');
                    UInt32 hash = CRC32_EncryptString(realPath);
                    UInt64 offset = tocEnd + offsetGLOB;

                    byte[] fileBytes = File.ReadAllBytes(file);

                    UInt64 size = (UInt64)fileBytes.Length;
                    UInt32 needsCompression = 0;
                    UInt32 dataType = 0;
                    // 
                    if (realPath.Contains(".gif")) {
                        needsCompression = 0;
                    }
                    else if (realPath.Contains(".png")) {
                        needsCompression = 0;
                    }
                    else if (realPath.Contains(".jpeg") ||
                        realPath.Contains(".jpg")) {
                        needsCompression = 0;
                    }
                    else if (realPath.Contains(".ogg")) {
                        // needsCompression = 1;
                    }
                    else if (realPath.Contains(".wav")) {
                        // needsCompression = 1;
                    }
                    else if (realPath.Contains(".vs")) {
                        // needsCompression = 1;
                    }
                    else if (realPath.Contains(".fs")) {
                        // needsCompression = 1;
                    }
                    // Hatch formats
                    else if (realPath.Contains(".bin")) {
                        dataType = 2;
                    }
                    else if (realPath.Contains(".ibc")) {
                        dataType = 2;
                    }
                    else if (realPath.Contains(".hbc")) {
                        dataType = 2;
                    }
                    else if (realPath.Contains(".hcm")) {
                        dataType = 2;
                    }

                    // Encrypt all files
                    if (encryptAllFiles)
                        dataType = 2;

                    UInt64 compressedSize = size;

                    if (needsCompression == 1) {
                        using (MemoryStream outMemoryStream = new MemoryStream())
                        using (ZOutputStream compress = new ZOutputStream(outMemoryStream, zlibConst.Z_BEST_COMPRESSION)) { // zlibConst.Z_DEFAULT_COMPRESSION
                            compress.Write(fileBytes, 0, fileBytes.Length);
                            compress.finish();

                            fileBytes = outMemoryStream.ToArray();
                            compressedSize = (UInt64)fileBytes.Length;
                        }
                    }

                    if (dataType == 2) {
                        CryptoXOR(ref fileBytes, realPath, false);
                    }

                    Console.WriteLine(hash.ToString("X8") + ": " + realPath + " (Size: " + size + ", Compressed: " + compressedSize + ")");

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

                ulong uncompressedTotalUhhh = uncompressedTotal;
                if (uncompressedTotalUhhh >= 1024)
                    uncompressedTotalUhhh /= 1024;
                if (uncompressedTotalUhhh >= 1024)
                    uncompressedTotalUhhh /= 1024;
                if (uncompressedTotalUhhh >= 1024)
                    uncompressedTotalUhhh /= 1024;

                string identifierUncomp = "B";
                if (uncompressedTotal >= 1024 * 1024 * 1024)
                    identifierUncomp = "GB";
                else if (uncompressedTotal >= 1024 * 1024)
                    identifierUncomp = "MB";
                else if (uncompressedTotal >= 1024)
                    identifierUncomp = "KB";

                ulong compressedTotalUhhh = uncompressedTotal;
                if (compressedTotalUhhh >= 1024)
                    compressedTotalUhhh /= 1024;
                if (compressedTotalUhhh >= 1024)
                    compressedTotalUhhh /= 1024;
                if (compressedTotalUhhh >= 1024)
                    compressedTotalUhhh /= 1024;

                string identifierComp = "B";
                if (compressedTotal >= 1024 * 1024 * 1024)
                    identifierComp = "GB";
                else if (compressedTotal >= 1024 * 1024)
                    identifierComp = "MB";
                else if (compressedTotal >= 1024)
                    identifierComp = "KB";

                Console.WriteLine();
                Console.WriteLine("Compressed " + uncompressedTotal + " bytes (" + uncompressedTotalUhhh + " " + identifierUncomp + ") to " + compressedTotal + " bytes (" + compressedTotalUhhh + " " + identifierComp + ")");
                Console.WriteLine();
            }

        }

        [STAThread]
        static void Main(string[] args) {
            string out_filename = "", resourcesFolder = "";

            if (args.Length >= 1)
                resourcesFolder = args[0];
            if (args.Length >= 2)
                out_filename = args[1];

            if (resourcesFolder == "") {
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
                    }
                    else {
                        return;
                    }
                }
            }
            if (out_filename == "") {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog()) {
                    saveFileDialog.InitialDirectory = resourcesFolder;
                    if (Properties.Settings1.Default.LastSave != "")
                        saveFileDialog.InitialDirectory = Properties.Settings1.Default.LastSave;

                    saveFileDialog.FileName = "Data.hatch";
                    saveFileDialog.Filter = "Hatch Data Pack (*.hatch)|*.hatch";
                    saveFileDialog.FilterIndex = 2;
                    saveFileDialog.RestoreDirectory = true;

                    if (saveFileDialog.ShowDialog() == DialogResult.OK) {
                        out_filename = saveFileDialog.FileName;

                        Properties.Settings1.Default.LastSave = Directory.GetParent(out_filename).FullName;
                        Properties.Settings1.Default.Save();
                    }
                    else {
                        return;
                    }
                }
            }


            resourcesFolder = resourcesFolder.Replace('\\', '/');
            if (resourcesFolder[resourcesFolder.Length - 1] != '/')
                resourcesFolder += "/";

            PackHatch1(out_filename, resourcesFolder);


            Console.WriteLine("Done!");

            while (Debugger.IsAttached) ;
        }
    }
}
