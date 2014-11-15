// a small tool to unzip an archive - but skip unchanged files while unzipping.
//
// mechanism: works by storing metadata (alt stream) containing an MD5 hash for each file.
// won't work on Windows shared drives (they're not NTFS file system)
// won't work on Linux until we add xattr support (exteneded attributes, similar to alt stream).
//
// credits:
// Trident.Core.IO.Ntfs: Richard Deeming's NTFS alternative data streams lib
// SharpZipLib: Mike Krueger (original author) / David Pierson (maintainer)
// Jiri Moudry (this file only) 2014

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Trinet.Core.IO.Ntfs;

namespace Unzip_SkipUnchanged
{
    class Prog_Unzip
    {
        const string STREAM_NAME = "unz_md5_hash";
        static readonly MD5 s_md5_lib = MD5.Create();

        static int Main(string[] args)
        {
            // parse + validate args:
            if (args.Length < 2 || args[0] != "x") { WriteUsageAndExit(); }
            string zipFileName = args.Last();
            Console.WriteLine("Processing archive: " + zipFileName);
            if (!File.Exists(zipFileName))
            {
                Console.WriteLine("Error: cannot find archive: " + zipFileName);
                return 2;
            }
            var destinationDir = (args[1].StartsWith("-o")) ? args[1].Substring(2) : ".";

            // open archive, prepare vars:
            ZipFile zipFile = new ZipFile(zipFileName);
            byte[] buffer = new byte[2048];
            int numExtracted = 0, numSkipped = 0;

            foreach (ZipEntry entry in zipFile)
            {
                var combinedPath = Path.Combine(destinationDir, entry.Name);
                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(combinedPath);
                    continue;
                }
                if (!entry.IsFile)
                    continue; // skip dirs
                var fileInfo = new FileInfo(combinedPath);
                string md5_str = ComputeStreamHash_MD5(zipFile.GetInputStream(entry));

                // check if md5 is same as in Alternate stream in existing file:
                string prev_md5 = TryReadAlternateStream(fileInfo, STREAM_NAME);
                if (prev_md5 == md5_str && fileInfo.Length == entry.Size)
                {
                    if (numSkipped++ == 0)
                        Console.WriteLine("Skip unchanged: " + fileInfo.FullName +  " (md5 match: "+md5_str + ")");
                    continue;
                }

                if (!fileInfo.Directory.Exists)
                    fileInfo.Directory.Create();

                // now extract the file, followed by the alt stream containing MD5:
                using (FileStream targetFileStr = fileInfo.OpenWrite())
                using (var zipStream = zipFile.GetInputStream(entry))
                {
                    Console.WriteLine("Extracting file " + fileInfo);
                    StreamUtils.Copy(zipStream, targetFileStr, buffer);
                }
                fileInfo.Refresh();
                using (FileStream altStream = fileInfo.GetAlternateDataStream(STREAM_NAME, FileMode.CreateNew).OpenWrite())
                using (StreamWriter writer = new StreamWriter(altStream))
                {
                    //Console.WriteLine("Writing file " + fileInfo + ":" + STREAM_NAME + " = " + md5_str);
                    writer.Write(md5_str);
                }
                numExtracted++;
            }
            Console.WriteLine("END archive, " + numExtracted  +" extracted, " + numSkipped + " skipped");
            return 0;
        }

        static void WriteUsageAndExit()
        {
            Console.WriteLine("Unzip_SkipUnchanged: ");
            Console.WriteLine("  an unzip tool that skips unchanged files");
            Console.WriteLine("works by calculating MD5 hash from zip file, and comparing it possible to alt stream of existing file");
            Console.WriteLine("Usage: (based on 7z):");
            Console.WriteLine("Unzip_SkipUnchanged x -o{OutputDir} <archive_name>");
            Console.WriteLine("  where 'x' means eXtract files with full paths");
            Environment.Exit(7);
        }

        static string ComputeStreamHash_MD5(Stream stream)
        {
            var bytes = s_md5_lib.ComputeHash(stream);
            string md5_str = Convert.ToBase64String(bytes);
            if (md5_str.EndsWith("=="))
                md5_str = md5_str.Substring(0, md5_str.Length - 2);
            return md5_str;
        }

        static string TryReadAlternateStream(FileInfo fileInfo, string streamName)
        {
            if (fileInfo.Exists && fileInfo.AlternateDataStreamExists(streamName))
            {
                using (var altStream = fileInfo.GetAlternateDataStream(streamName).OpenText())
                {
                    string prev_md5 = altStream.ReadToEnd();
                    return prev_md5;
                }
            }
            return null;
        }

    }
}