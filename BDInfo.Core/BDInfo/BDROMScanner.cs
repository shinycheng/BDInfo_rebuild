using BDCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

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

            Timer timer = null;
            try
            {
                ScanBDROMState scanState = new();
                scanState.OnReportChange += ScanBDROMProgress;

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
                        playlist.ClearBitrates();

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

                timer = new Timer(ScanBDROMEvent, scanState, 1000, 1000);

                foreach (TSStreamFile streamFile in streamFiles)
                {
                    scanState.StreamFile = streamFile;

                    Thread thread = new(ScanBDROMThread);
                    thread.Start(scanState);
                    while (thread.IsAlive)
                    {
                        Thread.Sleep(10);
                    }

                    if (streamFile.FileInfo != null)
                        scanState.FinishedBytes += streamFile.FileInfo.Length;
                    else
                        scanState.FinishedBytes += streamFile.FileInfo.Length;
                    if (scanState.Exception != null)
                    {
                        scanResult.FileExceptions[streamFile.Name] = scanState.Exception;
                    }
                }
                scanResult.ScanException = null;
            }
            catch (Exception ex)
            {
                scanResult.ScanException = ex;
                File.AppendAllText(errorLogPath, $"{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            finally
            {
                timer?.Dispose();
            }

            return scanResult;
        }

        private static void ScanBDROMProgress(ScanBDROMState scanState)
        {
            try
            {
                if (scanState.StreamFile == null)
                {
                    Console.Write("\rStarting Scan");
                }
                else
                {
                    Console.Write($"\rScanning {scanState.StreamFile.DisplayName}");
                }

                long finishedBytes = scanState.FinishedBytes;
                if (scanState.StreamFile != null)
                {
                    finishedBytes += scanState.StreamFile.Size;
                }

                double progress = ((double)finishedBytes / scanState.TotalBytes);
                double progressValue = Math.Clamp(100 * progress, 0, 100);

                TimeSpan elapsedTime = DateTime.Now.Subtract(scanState.TimeStarted);
                TimeSpan remainingTime;
                if (progress > 0 && progress < 1)
                {
                    remainingTime = new TimeSpan(
                            (long)((double)elapsedTime.Ticks / progress) - elapsedTime.Ticks);
                }
                else
                {
                    remainingTime = new TimeSpan(0);
                }

                Console.Write($" | Progress: {progressValue,6:F2}%");
                Console.Write($" | Elapsed: {elapsedTime.Hours:D2}:{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}");
                Console.Write($" | Remaining: {remainingTime.Hours:D2}:{remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}");
            }
            catch
            {
                // Suppress progress reporting errors
            }
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

        private static void ScanBDROMEvent(object state)
        {
            ScanBDROMProgress(state as ScanBDROMState);
        }

        private static void ScanBDROMThread(object parameter)
        {
            ScanBDROMState scanState = (ScanBDROMState)parameter;
            try
            {
                TSStreamFile streamFile = scanState.StreamFile;
                List<TSPlaylistFile> playlists = scanState.PlaylistMap[streamFile.Name];
                streamFile.Scan(playlists, true);
            }
            catch (Exception ex)
            {
                scanState.Exception = ex;
            }
        }
    }
}
