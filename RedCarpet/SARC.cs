﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Syroot.BinaryData;

/* My SARC library. Packing is not yet supported. */

namespace RedCarpet
{
    class SARC
    {
        public void pack(string Directory)
        {
            Stream o = new MemoryStream();
            BinaryDataWriter bw = new BinaryDataWriter(o, false);
            List<string> pFiles = new List<string>();
            List<byte[]> fileData = new List<byte[]>();

            readFiles(Directory, pFiles, fileData);
            calcPadding(pFiles);
            writeSARCChunk(bw);
        }

        public Dictionary<string, byte[]> unpackRam(Stream src)
        {
            Dictionary<string, byte[]> res = new Dictionary<string, byte[]>();
            BinaryDataReader br = new BinaryDataReader(src, false);
            br.BaseStream.Position = 0;
            br.ByteOrder = ByteOrder.BigEndian;
            br.ReadUInt32(); // Header
            br.ReadUInt16(); // Chunk length
            br.ReadUInt16(); // BOM
            br.ReadUInt32(); // File size
            UInt32 startingOff = br.ReadUInt32();
            br.ReadUInt32(); // Unknown;
            SFAT sfat = new SFAT();
            sfat.parse(br, (int)br.BaseStream.Position);
            SFNT sfnt = new SFNT();
            sfnt.parse(br, (int)br.BaseStream.Position, sfat, (int)startingOff);

            for (int m = 0; m < sfat.nodeCount; m++)
            {
                br.Seek(sfat.nodes[m].nodeOffset + startingOff, 0);
                byte[] temp;
                if (m == 0)
                {
                    temp = br.ReadBytes((int)sfat.nodes[m].EON);
                }
                else
                {
                    int tempInt = (int)sfat.nodes[m].EON - (int)sfat.nodes[m].nodeOffset;
                    temp = br.ReadBytes(tempInt);
                }
                res.Add(sfnt.fileNames[m], temp);
            }
            new SARC();
            return res;
        }

        public void unpack(string file)
        {
            try
            {
                Stream src = new MemoryStream(File.ReadAllBytes(file + ".sarc"));
                var files = unpackRam(src);
                write(files.Keys.ToList(), files.Values.ToList(), file);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public int calcPadding(List<string> file)
        {
            int tSize = 20 + 12 + 8;
            for (int i = 0; i < file.Count; i++)
            {
                tSize += 0x10;
                tSize += file[i].Length;
            }
            return tSize *= ((10 / 2));
        }

        public void writeSARCChunk(BinaryDataWriter bw)
        {
            bw.ByteOrder = ByteOrder.BigEndian;
            bw.Write("SARC");
            bw.Write((UInt16)0x14); // Chunk length
            bw.Write((UInt16)0xFEFF); // BOM
            bw.Write((UInt32)0); // File size

        }

        public void readFiles(string dir, List<string> flist, List<byte[]> fdata)
        {
            processDirectory(dir, flist, fdata);
        }

        public void processDirectory(string targetDirectory, List<string> flist, List<byte[]> fdata)
        {
            string[] fileEntries = Directory.GetFiles(targetDirectory);
            foreach (string fileName in fileEntries)
            {
                processFile(fileName, fdata);
                char[] sep = { '\\' };
                string[] fn = fileName.Split(sep);
                string tempf = "";
                for (int i = 1; i < fn.Length; i++)
                {
                    tempf += fn[i];
                    if (fn.Length > 2 && (i != fn.Length - 1))
                    {
                        tempf += "/";
                    }
                }
                flist.Add(tempf);
            }

            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
                processDirectory(subdirectory, flist, fdata);
        }

        public void processFile(string path, List<byte[]> fdata)
        {
            byte[] temp = File.ReadAllBytes(path);
            fdata.Add(temp);
        }

        public void write(List<string> fileNames, List<byte[]> files, string file)
        {
            Directory.CreateDirectory(file);
            
            for (int s = 0; s < fileNames.Count; s++)
            {
                if (fileNames[s].Contains("/"))
                {
                    char[] sep = { '/' };
                    string[] p = fileNames[s].Split(sep);
                    string fullDir = file + "/";
                    for (int r = 0; r < p.Length - 1; r++)
                    {
                        fullDir += p[r] + "/";
                        Directory.CreateDirectory(fullDir);
                    }
                }
                FileStream fs = File.Create(file + "\\" + fileNames[s]);
                fs.Write(files[s], 0, files[s].Length);
                fs.Close();
            }
        }

       public class SFAT
        {
            public List<Node> nodes = new List<Node>();

            public char[] chunkID;
            public UInt16 chunkSize;
            public UInt16 nodeCount;
            public UInt32 hashMultiplier;

            public class Node
            {
                public UInt32 hash;
                public byte fileBool;
                public byte unknown1;
                public UInt16 fileNameOffset;
                public UInt32 nodeOffset;
                public UInt32 EON;
            }

            public void parse(BinaryDataReader br, int pos)
            {
                br.ReadUInt32(); // Header;
                chunkSize = br.ReadUInt16();
                nodeCount = br.ReadUInt16();
                hashMultiplier = br.ReadUInt32();
                for (int i = 0; i < nodeCount; i++)
                {
                    Node node = new Node();
                    node.hash = br.ReadUInt32();
                    node.fileBool = br.ReadByte();
                    node.unknown1 = br.ReadByte();
                    node.fileNameOffset = br.ReadUInt16();
                    node.nodeOffset = br.ReadUInt32();
                    node.EON = br.ReadUInt32();
                    nodes.Add(node);
                }
            }
        }

        public class SFNT
        {
            public List<string> fileNames = new List<string>();

            public UInt32 chunkID;
            public UInt16 chunkSize;
            public UInt16 unknown1;
            
            public void parse(BinaryDataReader br, int pos, SFAT sfat, int start)
            {
                chunkID = br.ReadUInt32();
                chunkSize = br.ReadUInt16();
                unknown1 = br.ReadUInt16();

                char[] temp = br.ReadChars(start - (int)br.BaseStream.Position);
                string temp2 = new string(temp);
                char[] splitter = { (char)0x00 };
                string[] names = temp2.Split(splitter);
                for (int j = 0; j < names.Length; j++)
                {
                    if (names[j] != "")
                    {
                        fileNames.Add(names[j]);
                    }
                }
            }
        }
    }
}