using BDCommon;
using System;
using System.Collections.Generic;
using System.IO;

namespace BDInfo
{
    internal static class BDROMInitializer
    {
        internal static BDROM InitBDROM(string path, BDInfoSettings settings, string errorLogPath)
        {
            var result = InitBDROMWork(path, settings, errorLogPath);
            if (result is Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }

            BDROM bdrom = (BDROM)result;
            PrintBDROMInfo(bdrom);
            return bdrom;
        }

        private static object InitBDROMWork(string path, BDInfoSettings settings, string errorLogPath)
        {
            try
            {
                BDROM bdrom = new BDROM(path, settings);
                bdrom.StreamClipFileScanError += BDROM_StreamClipFileScanError;
                bdrom.StreamFileScanError += BDROM_StreamFileScanError;
                bdrom.PlaylistFileScanError += BDROM_PlaylistFileScanError;
                bdrom.Scan();
                return bdrom;
            }
            catch (Exception ex)
            {
                File.AppendAllText(errorLogPath, $"{ex}{Environment.NewLine}{Environment.NewLine}");
                return ex;
            }
        }

        private static bool BDROM_StreamClipFileScanError(TSStreamClipFile streamClipFile, Exception ex)
        {
            Console.WriteLine($"An error occurred while scanning the stream clip file {streamClipFile.Name}.");
            Console.WriteLine("The disc may be copy-protected or damaged.");
            Console.WriteLine("Will continue scanning the stream clip files.");

            return true;
        }

        private static bool BDROM_StreamFileScanError(TSStreamFile streamFile, Exception ex)
        {
            Console.WriteLine($"An error occurred while scanning the stream file {streamFile.Name}.");
            Console.WriteLine("The disc may be copy-protected or damaged.");
            Console.WriteLine("Will continue scanning the stream files.");

            return true;
        }

        private static bool BDROM_PlaylistFileScanError(TSPlaylistFile playlistFile, Exception ex)
        {
            Console.WriteLine($"An error occurred while scanning the playlist file {playlistFile.Name}.");
            Console.WriteLine("The disc may be copy-protected or damaged.");
            Console.WriteLine("Will continue scanning the playlist files.");

            return true;
        }

        private static void PrintBDROMInfo(BDROM bdrom)
        {
            Console.WriteLine($"Detected BDMV Folder: {bdrom.DirectoryBDMV.FullName}");
            Console.WriteLine($"Disc Title: {bdrom.DiscTitle}");
            Console.WriteLine($"Disc Label: {bdrom.VolumeLabel}");

            List<string> features = [];
            if (bdrom.IsUHD)
            {
                features.Add("Ultra HD");
            }
            if (bdrom.Is50Hz)
            {
                features.Add("50Hz Content");
            }
            if (bdrom.IsBDPlus)
            {
                features.Add("BD+ Copy Protection");
            }
            if (bdrom.IsBDJava)
            {
                features.Add("BD-Java");
            }
            if (bdrom.Is3D)
            {
                features.Add("Blu-ray 3D");
            }
            if (bdrom.IsDBOX)
            {
                features.Add("D-BOX Motion Code");
            }
            if (bdrom.IsPSP)
            {
                features.Add("PSP Digital Copy");
            }
            if (features.Count > 0)
            {
                Console.WriteLine($"Detected Features: {string.Join(", ", [.. features])}");
            }

            Console.WriteLine($"Disc Size: {bdrom.Size:N0} bytes ({ToolBox.FormatFileSize(bdrom.Size, true)})");
            Console.WriteLine();
        }
    }
}
