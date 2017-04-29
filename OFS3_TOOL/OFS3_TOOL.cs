using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;

// Icon made by http://www.freepik.com from http://www.flaticon.com.

namespace OFS3_TOOL
{
    public partial class OFS3_TOOL : Form
    {
        public OFS3_TOOL()
        {
            InitializeComponent();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/Liquid-S");
        }

        private void DecompressGZip(string CompressedFile)
        {
            string DecompressedFile = CompressedFile + ".decompressed";

            // Check out if the decompressed file is a ".ofs3" and saves it's extension.
            using (FileStream CompressedFileStream = new FileStream(CompressedFile, FileMode.Open, FileAccess.Read))
              // If decompressed file's MagicID == OFS3, then add ".ofs3" to it's extension...
                using (GZipStream DecompressionStream = new GZipStream(CompressedFileStream, CompressionMode.Decompress))
                {
                    byte[] Magic = new byte[4];
                    DecompressionStream.Read(Magic, 0, 4);

                    if (Magic[0] == 0x4F && Magic[1] == 0x46 && Magic[2] == 0x53 && Magic[3] == 0x33)
                        DecompressedFile += ".ofs3";
                }

            // Decompress the file and save it.
            using (FileStream CompressedFileStream = new FileStream(CompressedFile, FileMode.Open, FileAccess.Read))
            using (GZipStream DecompressionStream = new GZipStream(CompressedFileStream, CompressionMode.Decompress))
            using (FileStream DecompressedFileStream = File.Create(DecompressedFile))
                DecompressionStream.CopyTo(DecompressedFileStream);
            
            File.Delete(CompressedFile); // Delete the original/compressed file because we don't need it anymore.

            // If new file's MagicID == OFS3, then extract everything from it.
            if (uSERECURSIONToolStripMenuItem.Checked == true && Path.GetExtension(DecompressedFile) == ".ofs3")
                UnpackOFS3(DecompressedFile, Path.Combine(Path.GetDirectoryName(DecompressedFile), "EXTRACTED_" + Path.GetFileName(DecompressedFile)));
        }

        private void button1_Click(object sender, EventArgs e) //EXTRACT
        {
            using (OpenFileDialog OFS3Files = new OpenFileDialog())
            {
                OFS3Files.Title = "Select one or more files ";
                OFS3Files.Multiselect = true;

                if (OFS3Files.ShowDialog() == DialogResult.OK)
                {
                    label1.Text = "Wait..."; // Change "Ready!" to "Wait..."
                    label1.Refresh(); // Refresh the Status label.

                    foreach (string SingleOFS3File in OFS3Files.FileNames)
                        UnpackOFS3(SingleOFS3File, Path.Combine("EXTRACTED", "EXTRACTED_" + Path.GetFileName(SingleOFS3File)));

                    label1.Text = "Ready!"; // Change the "Status" to "Ready!".
                    MessageBox.Show("Done!", "Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        public void UnpackOFS3(string OFS3File, string DestinationDir)
        {
            using (FileStream OFS3 = new FileStream(OFS3File, FileMode.Open, FileAccess.Read))
            using (BinaryReader OFS3Reader = new BinaryReader(OFS3, Encoding.Default))
            {
                //Check if MagicID == OFS3.
                if (OFS3Reader.ReadUInt32() == 0x3353464F)
                {
                    // Create DestinationDir.
                    if (Directory.Exists(DestinationDir) == false)
                        Directory.CreateDirectory(DestinationDir);

                    OFS3Reader.ReadUInt32(); // Header.
                    ushort Type = OFS3Reader.ReadUInt16();
                    byte Padding = OFS3Reader.ReadByte(),
                    // There are two Types of Type "0x00": One has SubType "0x00" (this specifies the size of each file) and the other has "0x01" (this doesn't).
                    SubType = OFS3Reader.ReadByte();
                    uint Size = OFS3Reader.ReadUInt32(),
                    NFiles = OFS3Reader.ReadUInt32();

                    uint[] ExTractedFileOffset = new uint[NFiles],
                    ExtractedFileSize = new uint[NFiles],
                    FileNameOffset = new uint[NFiles];

                    // Read offset, the size and the nameoffset of each file.
                    for (int i = 0; i < NFiles; i++)
                    {
                        ExTractedFileOffset[i] = OFS3Reader.ReadUInt32() + 0x10;

                        ExtractedFileSize[i] = OFS3Reader.ReadUInt32();

                        if (Type == 0x02)
                            FileNameOffset[i] = OFS3Reader.ReadUInt32() + 0x10;
                    }

                    // We need to save "some data" if we want to be able to rebuild the file in a faultless way.
                    // This way the tool will be able to rebuild the OFSE3 depending on the Type and the padding.
                    using (FileStream InfoFile = new FileStream(Path.Combine(DestinationDir, "DONT_Delete_Me.DONT_Delete_Me"), FileMode.Create, FileAccess.Write))
                    using (BinaryWriter InfoFileWriter = new BinaryWriter(InfoFile))
                    {
                        InfoFileWriter.Write(Type);
                        InfoFileWriter.Write(Padding);
                        InfoFileWriter.Write(SubType);

                        // Files with SubType == 1 doesn't specify the size of each file, instead of the size there are just a bunch of 0x0000 or a counter that increments for each file.
                        if (SubType == 1)
                        {
                            InfoFileWriter.Write(NFiles);
                            for (int i = 0; i < ExtractedFileSize.Length; i++)
                                InfoFileWriter.Write(ExtractedFileSize[i]);
                        }
                    }

                    for (int i = 0; i < NFiles; i++)
                    {
                        // Files with SubType == 1 doesn't specify the size of each file, therefore we need to calculate it.
                        if (SubType == 1)
                        {
                            if ((ExTractedFileOffset[i] != 0 && i == NFiles - 1))
                                ExtractedFileSize[i] = (uint)OFS3.Length - ExTractedFileOffset[i];
                            else if (ExTractedFileOffset[i] != 0 && i < NFiles - 1)
                                ExtractedFileSize[i] = ExTractedFileOffset[i + 1] - ExTractedFileOffset[i];
                        }

                        // Reads and saves the name of each file.
                        string NewFileName = null;
                        if (Type == 0x02)
                        {
                            OFS3.Seek(FileNameOffset[i], SeekOrigin.Begin);

                            ushort Chara = 1;

                            while (OFS3.Position != OFS3.Length)
                            {
                                Chara = OFS3Reader.ReadByte();

                                if (Chara != 0)
                                    NewFileName += (char)Chara;
                                else
                                    break;
                            }

                            NewFileName = Path.Combine(DestinationDir, "(" + i.ToString("D4") + ")_") + NewFileName;
                        }
                        else
                            NewFileName += Path.Combine(DestinationDir, "(" + i.ToString("D4") + ")");

                        // Read the new file's data and save it in "NewFileBody".
                        OFS3.Seek(ExTractedFileOffset[i], SeekOrigin.Begin);
                        byte[] NewFileBody = new byte[ExtractedFileSize[i]];
                        OFS3.Read(NewFileBody, 0, NewFileBody.Length);

                        // Establish the file's extension.
                        if (NewFileBody.Length > 8)
                        {
                            if (!Path.GetFileName(NewFileName).Contains(".ofs3") && (NewFileBody[0] == 0x4F && NewFileBody[1] == 0x46 && NewFileBody[2] == 0x53 && NewFileBody[3] == 0x33))
                                NewFileName += ".ofs3";
                            else if (Type == 0 && (NewFileBody[0] == 0x1F && NewFileBody[1] == 0x8B && NewFileBody[2] == 0x08))
                                NewFileName += ".gz";
                            else if (Type == 0 && (NewFileBody[0] == 0x4F && NewFileBody[1] == 0x4D && NewFileBody[2] == 0x47 && NewFileBody[3] == 0x2E && NewFileBody[4] == 0x30 && NewFileBody[5] == 0x30))
                                NewFileName += ".gmo";
                            else if (Type == 0 && (NewFileBody[0] == 0x4D && NewFileBody[1] == 0x49 && NewFileBody[2] == 0x47 && NewFileBody[3] == 0x2E && NewFileBody[4] == 0x30 && NewFileBody[5] == 0x30))
                                NewFileName += ".gim";
                            else if (Type == 0 && (NewFileBody[0] == 0x54 && NewFileBody[1] == 0x49 && NewFileBody[2] == 0x4D && NewFileBody[3] == 0x32))
                                NewFileName += ".tm2";
                            else if (Type == 0 && (NewFileBody[0] == 0x50 && NewFileBody[1] == 0x49 && NewFileBody[2] == 0x4D && NewFileBody[3] == 0x32))
                                NewFileName += ".pm2";
                        }

                        // Save the extracted file in DestinationDir.
                        using (FileStream NewFile = new FileStream(NewFileName, FileMode.Create, FileAccess.Write))
                            NewFile.Write(NewFileBody, 0, NewFileBody.Length);

                        // Recursion time! Check if the new file is a GZip or a OFS3.
                        if (NewFileBody.Length > 4)
                        {
                            if (gZIPToolStripMenuItem.Checked == true && (NewFileBody[0] == 0x1F && NewFileBody[1] == 0x8B && NewFileBody[2] == 0x08))
                                DecompressGZip(NewFileName);

                            else if (uSERECURSIONToolStripMenuItem.Checked == true && (NewFileBody[0] == 0x4F && NewFileBody[1] == 0x46 && NewFileBody[2] == 0x53 && NewFileBody[3] == 0x33))
                                UnpackOFS3(NewFileName, Path.Combine(Path.GetDirectoryName(NewFileName), "EXTRACTED_" + Path.GetFileName(NewFileName)));
                        }
                    }
                }
            }
        }

        private void button2_Click(object sender, EventArgs e) //REPACK
        {
            string OriginalDir = "EXTRACTED";

            // If "EXTRACTED" exists and it's not empty.
            if (Directory.Exists(OriginalDir) && Directory.EnumerateDirectories(OriginalDir).Any() == true)
            {
                label1.Text = "Wait..."; // Change "Ready!" to "Wait..."
                label1.Refresh(); // Refresh the Status label.

                // Clone the directories, this way the tool can delete, change and repack everything without worrying about damage the user work.
                string TEMPFolder = "TEMP001";
                CloneDirectory(OriginalDir, TEMPFolder);

                // For each OFS3 folder to be repacked. Ex: DirToBeRepacked = "EXTRACTED\\DATA_TIME\\scp007"
                foreach (string DirToBeRepacked in Directory.GetDirectories(TEMPFolder, "*", SearchOption.TopDirectoryOnly))
                    if (Directory.EnumerateFiles(DirToBeRepacked).Any() == true)
                        RepackOFS3(DirToBeRepacked, "REPACKED");

                // Remove the temp folder as it's no longer needed.
                Directory.Delete(TEMPFolder, true);
                while (Directory.Exists(TEMPFolder)) { }

                label1.Text = "Ready!"; // Change the "Status" to "Ready!".
                MessageBox.Show("Done!", "Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RepackOFS3(string DirToBeRepacked, string DestinationDir)
        {
            // Create the Dir where the new file will be saved.
            if (Directory.Exists(DestinationDir) == false)
                Directory.CreateDirectory(DestinationDir);

            // Check if DirToBeRepacked has SubDris to be repacked.
            foreach (string SubDir in Directory.GetDirectories(DirToBeRepacked))
                RepackOFS3(SubDir, Path.GetDirectoryName(SubDir));

            // Check if DirToBeRepacked has files to be compressed in GZip.
            if (gZIPToolStripMenuItem.Checked == true)
            {
                foreach (string FileToBeCompressed in Directory.GetFiles(DirToBeRepacked, "*.decompressed", SearchOption.TopDirectoryOnly))
                    CompressToGZip(FileToBeCompressed);

                foreach (string FileToBeCompressed in Directory.GetFiles(DirToBeRepacked, "*.decompressed.ofs3", SearchOption.TopDirectoryOnly))
                    CompressToGZip(FileToBeCompressed);

            }

            ushort Type = 0;
            byte Padding = 0,
            SubType = 0; // I'm not really sure about this, but it seems to work.

            uint[] FilesSizes = null;

            // Reads and save the data inside "DONT_Delete_Me.DONT_Delete_Me". Thanks to this I know the Type and the Unknown data I need to rebuild the file.
            using (FileStream DDM = new FileStream(Path.Combine(DirToBeRepacked, "DONT_Delete_Me.DONT_Delete_Me"), FileMode.Open, FileAccess.Read))
            using (BinaryReader DDMReader = new BinaryReader(DDM, Encoding.Default))
            {
                Type = DDMReader.ReadUInt16();
                Padding = DDMReader.ReadByte();
                SubType = DDMReader.ReadByte();
                if (SubType == 1)
                {
                    FilesSizes = new uint[DDMReader.ReadUInt32()];
                    for (int i = 0; i < FilesSizes.Length; i++)
                        FilesSizes[i] = DDMReader.ReadUInt32();
                }
            }

            File.Delete(Path.Combine(DirToBeRepacked, "DONT_Delete_Me.DONT_Delete_Me"));
            while (File.Exists(Path.Combine(DirToBeRepacked, "DONT_Delete_Me.DONT_Delete_Me"))) { }

            // Reads and save the address of each file inside "DirToBeRepacked".
            string[] FullFilesAddress = Directory.GetFiles(DirToBeRepacked, "*", SearchOption.TopDirectoryOnly);
            string[] CleanedFilesAddress = CleanAddress(Directory.GetFiles(DirToBeRepacked, "*", SearchOption.TopDirectoryOnly), DirToBeRepacked);

            Array.Sort(FullFilesAddress, new AlphanumComparatorFast());

            uint[] FilesOffsets = new uint[FullFilesAddress.Length],
            FileNamesOffsets = new uint[FullFilesAddress.Length];

            if (FilesSizes == null)
                FilesSizes = new uint[FullFilesAddress.Length];

            // START to build the new file.
            using (FileStream OFS3 = new FileStream(Path.Combine(DestinationDir, Path.GetFileName(DirToBeRepacked).Replace("EXTRACTED_", null)), FileMode.Create, FileAccess.Write))
            using (BinaryWriter OFS3Writer = new BinaryWriter(OFS3))
            {
                // HEADER - START
                OFS3Writer.Write((uint)0x3353464F); // Magic
                OFS3Writer.Write((uint)0x10); // HeaderSize
                OFS3Writer.Write((ushort)Type);
                OFS3Writer.Write((byte)Padding);
                OFS3Writer.Write((byte)SubType);
                OFS3Writer.Write((uint)0); //New file size. ATM is unknown!
                OFS3Writer.Write((uint)FullFilesAddress.Length); // Number of files inside "DestinationDir".
                                                                 // HEADER - END

                /* Write the pointer zone, but only with zeros because the size of each file is still unknown.
                 This zone will be overwritten later with the right sizes. */
                for (int i = 0; i < FullFilesAddress.Length; i++)
                {
                    OFS3Writer.Write((uint)0); // FileToBeInserted's Offset
                    OFS3Writer.Write((uint)0); // FileToBeInserted's Size
                    if (Type == 0x02)
                        OFS3Writer.Write((uint)0); // FileToBeInserted's NameOffset
                }

                // Padding time!
                if (OFS3.Position % Padding != 0)
                    while (OFS3.Position % Padding != 0)
                        OFS3Writer.Write((byte)0);

                // Time to insert each file inside the new file.
                for (int i = 0; i < FullFilesAddress.Length; i++)
                {
                    FilesOffsets[i] = (uint)OFS3.Position - 0x10; // Saves the offset's file.
                    using (FileStream FileToBeInserted = new FileStream(FullFilesAddress[i], FileMode.Open, FileAccess.Read))
                    {
                        byte[] NewFileBody = new byte[FileToBeInserted.Length];
                        FileToBeInserted.Read(NewFileBody, 0, NewFileBody.Length);
                        OFS3Writer.Write(NewFileBody, 0, NewFileBody.Length); // Save the file within the new file.
                        if (SubType != 1)
                            FilesSizes[i] = (uint)NewFileBody.Length;
                    }

                    // Padding time!
                    if (OFS3.Position % Padding != 0)
                        while (OFS3.Position % Padding != 0)
                            OFS3Writer.Write((byte)0);
                }

                // If Type == 0x02 then files'names must be saved at the end.
                if (Type == 0x02)
                {
                    for (int i = 0; i < FullFilesAddress.Length; i++)
                    {
                        FileNamesOffsets[i] = (uint)OFS3.Position - 0x10;
                        OFS3Writer.Write(CleanedFilesAddress[i].ToCharArray()); // Write file's name.
                        OFS3Writer.Write((byte)0x0);
                    }

                    // Padding time!
                    if (OFS3.Position % 0x10 != 0)
                        while (OFS3.Position % 0x10 != 0)
                            OFS3Writer.Write((byte)0);
                }

                // Returns at the beginning of the file to overwrite the zeros with the right data.
                OFS3.Seek(0xC, SeekOrigin.Begin);

                // Write the file's size.
                if (Type == 0)
                    OFS3Writer.Write((uint)OFS3.Length - 0x10);
                else if (Type == 0x02)
                    OFS3Writer.Write(FileNamesOffsets[0]); // The file's size ignore the files'names zone.

                OFS3.Seek(0x4, SeekOrigin.Current);

                // Time to insert the offset and size of each file.
                for (int i = 0; i < FullFilesAddress.Length; i++)
                {
                    // Some files doesn't have any data inside, so their offset must be setted to 0, but only if SubType != 1.                    
                    if (FilesSizes[i] == 0 && SubType != 1)
                        OFS3Writer.Write((uint)0);
                    else
                        OFS3Writer.Write(FilesOffsets[i]);

                    OFS3Writer.Write(FilesSizes[i]); // Write the file's size.

                    if (Type == 2) // Write the name's offset.
                        OFS3Writer.Write(FileNamesOffsets[i]);
                }
            }
        }

        public void CompressToGZip(string FileToBeCompressed)
        {
            using (FileStream DecompressedFileStream = new FileStream(FileToBeCompressed, FileMode.Open, FileAccess.Read))
            using (FileStream CompressedFileStream = File.Create(FileToBeCompressed.Replace(".decompressed", null)))
            using (GZipStream CompressionStream = new GZipStream(CompressedFileStream, CompressionLevel.Fastest))
                DecompressedFileStream.CopyTo(CompressionStream);

            File.Delete(FileToBeCompressed);
            while (File.Exists(FileToBeCompressed)) { }
        }

        // AlphanumComparatorFast taken from https://gist.github.com/ngbrown/3842065
        public class AlphanumComparatorFast : IComparer
        {
            public int Compare(object x, object y)
            {
                string s1 = x as string;
                if (s1 == null)
                    return 0;

                string s2 = y as string;
                if (s2 == null)
                    return 0;

                int len1 = s1.Length, len2 = s2.Length;
                int marker1 = 0, marker2 = 0;

                // Walk through two the strings with two markers.
                while (marker1 < len1 && marker2 < len2)
                {
                    char ch1 = s1[marker1], ch2 = s2[marker2];

                    // Some buffers we can build up characters in for each chunk.
                    char[] space1 = new char[len1], space2 = new char[len2];
                    int loc1 = 0, loc2 = 0;

                    // Walk through all following characters that are digits or
                    // characters in BOTH strings starting at the appropriate marker.
                    // Collect char arrays.
                    do
                    {
                        space1[loc1++] = ch1;
                        marker1++;

                        if (marker1 < len1)
                            ch1 = s1[marker1];
                        else
                            break;

                    } while (char.IsDigit(ch1) == char.IsDigit(space1[0]));

                    do
                    {
                        space2[loc2++] = ch2;
                        marker2++;

                        if (marker2 < len2)
                            ch2 = s2[marker2];
                        else
                            break;

                    } while (char.IsDigit(ch2) == char.IsDigit(space2[0]));

                    // If we have collected numbers, compare them numerically.
                    // Otherwise, if we have strings, compare them alphabetically.
                    string str1 = new string(space1), str2 = new string(space2);

                    int result;

                    if (char.IsDigit(space1[0]) && char.IsDigit(space2[0]))
                    {
                        int thisNumericChunk = int.Parse(str1);
                        int thatNumericChunk = int.Parse(str2);
                        result = thisNumericChunk.CompareTo(thatNumericChunk);
                    }
                    else
                        result = str1.CompareTo(str2);


                    if (result != 0)
                        return result;

                }
                return len1 - len2;
            }
        }
        // Clean all that is before the files'names, turns "\\" to "/" and order the string[] alphanumerically.
        private string[] CleanAddress(string[] stringa, string folder)
        {
            Array.Sort(stringa, new AlphanumComparatorFast());

            for (int i = 0; i < stringa.Length; i++)
                stringa[i] = stringa[i].Replace(folder + "\\", null).Replace("\\", "/").Replace("(" + i.ToString("D4") + ")_", null).Replace(".ofs3", null);

            return stringa;
        }

        // Clone the directories, this way the tool can delete, change and repack everything without worrying about damage the user work.
        private void CloneDirectory(string OriginalFolder, string TEMPFolder)
        {
            DirectoryInfo source = new DirectoryInfo(OriginalFolder),
                target = new DirectoryInfo(TEMPFolder);

            // Delte the TEMPDir if it already exist.
            if (Directory.Exists(target.FullName) == true)
            {
                Directory.Delete(target.FullName, true);
                while (Directory.Exists(target.FullName)) { }
            }

            // Create the TEMPDir and make it invisible.
            DirectoryInfo NewTEMPDir = Directory.CreateDirectory(target.FullName);
            NewTEMPDir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;

            // Copy the files to the TEMP folder.
            foreach (FileInfo fi in source.GetFiles())
                fi.CopyTo(Path.Combine(target.ToString(), fi.Name), true);

            // Copy the subfolders and their contents.
            foreach (string SubDir in Directory.GetDirectories(OriginalFolder, "*", SearchOption.TopDirectoryOnly))
                CloneDirectory(SubDir, Path.Combine(TEMPFolder, Path.GetFileName(SubDir)));
        }
    }
}
