using BDCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BDInfo
{
    internal static class BDROMScanner
    {
        internal static ScanBDROMResult ScanBDROM(BDROM bdrom, BDInfoSettings settings, string productVersion, string errorLogPath, string debugLogPath)
        {
            if (bdrom is null)
            {
                throw new Exception("BDROM is null");
            }

            List<TSStreamFile> streamFiles = new(bdrom.StreamFiles.Values);

            ScanBDROMResult scanResult = ScanBDROMWork(streamFiles, bdrom, settings, errorLogPath);
            ScanBDROMCompleted(scanResult, bdrom, settings, productVersion, errorLogPath, debugLogPath);
            return scanResult;
        }

        private static ScanBDROMResult ScanBDROMWork(List<TSStreamFile> streamFiles, BDROM bdrom, BDInfoSettings settings, string errorLogPath)
        {
            ScanBDROMResult scanResult = new ScanBDROMResult { ScanException = new Exception("Scan is still running.") };

            try
            {
                ScanBDROMState scanState = new();

                // --- Preparation phase (serial) ---

                // Calculate TotalBytes and build PlaylistMap
                foreach (TSStreamFile streamFile in streamFiles)
                {
                    if (settings.EnableSSIF &&
                            streamFile.InterleavedFile != null)
                    {
                        if (streamFile.InterleavedFile.FileInfo != null)
                            scanState.TotalBytes += streamFile.InterleavedFile.FileInfo.Length;
                        else
                            scanState.TotalBytes += streamFile.InterleavedFile.FileInfo.Length;
                    }
                    else
                    {
                        if (streamFile.FileInfo != null)
                            scanState.TotalBytes += streamFile.FileInfo.Length;
                        else
                            scanState.TotalBytes += streamFile.FileInfo.Length;
                    }

                    if (!scanState.PlaylistMap.ContainsKey(streamFile.Name))
                    {
                        scanState.PlaylistMap[streamFile.Name] = [];
                    }

                    foreach (TSPlaylistFile playlist in bdrom.PlaylistFiles.Values)
                    {
                        foreach (TSStreamClip clip in playlist.StreamClips)
                        {
                            if (clip.Name == streamFile.Name)
                            {
                                if (!scanState.PlaylistMap[streamFile.Name].Contains(playlist))
                                {
                                    scanState.PlaylistMap[streamFile.Name].Add(playlist);
                                }
                            }
                        }
                    }
                }

                // ClearBitrates once per playlist (before parallel scan)
                HashSet<TSPlaylistFile> clearedPlaylists = new();
                foreach (TSPlaylistFile playlist in bdrom.PlaylistFiles.Values)
                {
                    if (clearedPlaylists.Add(playlist))
                    {
                        playlist.ClearBitrates();
                    }
                }

                // --- Parallel scan phase ---
                ThreadSafeProgressReporter reporter = new(scanState.TotalBytes);

                ParallelOptions parallelOptions = new()
                {
                    MaxDegreeOfParallelism = settings.MaxThreads
                };

                Parallel.ForEach(streamFiles, parallelOptions, streamFile =>
                {
                    try
                    {
                        List<TSPlaylistFile> playlists = scanState.PlaylistMap[streamFile.Name];
                        streamFile.Scan(playlists, true);
                    }
                    catch (Exception ex)
                    {
                        scanResult.FileExceptions[streamFile.Name] = ex;
                    }
                    finally
                    {
                        long fileBytes = streamFile.FileInfo != null ? streamFile.FileInfo.Length : 0;
                        reporter.ReportFileCompleted(fileBytes);
                    }
                });

                reporter.RenderFinal();
                scanResult.ScanException = null;
            }
            catch (Exception ex)
            {
                scanResult.ScanException = ex;
                File.AppendAllText(errorLogPath, $"{ex}{Environment.NewLine}{Environment.NewLine}");
            }

            return scanResult;
        }

        private static void ScanBDROMCompleted(ScanBDROMResult scanResult, BDROM bdrom, BDInfoSettings settings, string productVersion, string errorLogPath, string debugLogPath)
        {
            Console.WriteLine();

            if (scanResult.ScanException != null)
            {
                Console.WriteLine("Scan complete.");
                Console.WriteLine($"{scanResult.ScanException.Message}");
                File.AppendAllText(errorLogPath, $"{scanResult.ScanException}{Environment.NewLine}{Environment.NewLine}");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(settings.ReportFileName))
                {
                    Console.WriteLine("Scan complete.");
                    ReportGenerator.GenerateReport(bdrom, scanResult, settings, productVersion, debugLogPath);
                }
                else if (scanResult.FileExceptions.Count > 0)
                {
                    Console.WriteLine("Scan completed with errors (see report).");
                }
                else
                {
                    Console.WriteLine("Scan completed successfully.");
                }
            }
        }
    }
}
