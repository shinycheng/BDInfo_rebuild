using BDCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BDInfo
{
    internal static class ReportGenerator
    {
        internal static void GenerateReport(BDROM BDROM, ScanBDROMResult scanResult, BDInfoSettings settings, string productVersion, string errorLogPath)
        {
            IEnumerable<TSPlaylistFile> playlists = BDROM.PlaylistFiles.OrderByDescending(s => s.Value.FileSize).Select(s => s.Value);

            try
            {
                Generate(BDROM, playlists, scanResult, settings, productVersion, errorLogPath);
            }
            catch (Exception ex)
            {
                File.AppendAllText(errorLogPath, $"{ex}{Environment.NewLine}{Environment.NewLine}");
                Console.WriteLine(ex.Message);
            }
        }

        private static void Generate(BDROM BDROM, IEnumerable<TSPlaylistFile> playlists, ScanBDROMResult scanResult, BDInfoSettings settings, string productVersion, string debugLogPath)
        {
            string reportName = Regex.IsMatch(settings.ReportFileName, @"\{\d+\}", RegexOptions.IgnoreCase) ?
                string.Format(settings.ReportFileName, BDROM.VolumeLabel) :
                settings.ReportFileName;

            if (!Regex.IsMatch(reportName, @"\.(\w+)$", RegexOptions.IgnoreCase))
            {
                reportName = $"{reportName}.bdinfo";
            }

            if (File.Exists(reportName))
            {
                // creates a backup
                File.Move(reportName, $"{reportName}{Guid.NewGuid()}");
            }

            using StreamWriter sw = File.AppendText(reportName);
            string protection = BDROM.IsBDPlus ? "BD+" : (BDROM.IsUHD ? "AACS2" : "AACS");

            if (!string.IsNullOrEmpty(BDROM.DiscTitle))
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-16}{1}", "Disc Title:",
                                                                BDROM.DiscTitle));
            sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-16}{1}", "Disc Label:",
                                                                    BDROM.VolumeLabel));
            sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-16}{1:N0} bytes", "Disc Size:",
                                                                    BDROM.Size));
            sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-16}{1}", "Protection:",
                                                                    protection));

            List<string> extraFeatures = [];
            if (BDROM.IsUHD)
            {
                extraFeatures.Add("Ultra HD");
            }
            if (BDROM.IsBDJava)
            {
                extraFeatures.Add("BD-Java");
            }
            if (BDROM.Is50Hz)
            {
                extraFeatures.Add("50Hz Content");
            }
            if (BDROM.Is3D)
            {
                extraFeatures.Add("Blu-ray 3D");
            }
            if (BDROM.IsDBOX)
            {
                extraFeatures.Add("D-BOX Motion Code");
            }
            if (BDROM.IsPSP)
            {
                extraFeatures.Add("PSP Digital Copy");
            }
            if (extraFeatures.Count > 0)
            {
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "{0,-16}{1}", "Extras:",
                                                                        string.Join(", ", [.. extraFeatures])));
            }
            sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-16}{1}", "BDInfo:",
                                                                    productVersion));

            sw.WriteLine(Environment.NewLine);

            if (settings.IncludeVersionAndNotes)
            {
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "{0,-16}{1}", "Notes:", ""));
                sw.WriteLine(Environment.NewLine);
                sw.WriteLine("BDINFO HOME:");
                sw.WriteLine("  Cinema Squid (old)");
                sw.WriteLine("    http://www.cinemasquid.com/blu-ray/tools/bdinfo");
                sw.WriteLine("  UniqProject GitHub (new)");
                sw.WriteLine("   https://github.com/UniqProject/BDInfo");
                sw.WriteLine(Environment.NewLine);
                sw.WriteLine("INCLUDES FORUMS REPORT FOR:");
                sw.WriteLine("  AVS Forum Blu-ray Audio and Video Specifications Thread");
                sw.WriteLine("    http://www.avsforum.com/avs-vb/showthread.php?t=1155731");
                sw.WriteLine(Environment.NewLine);
            }

            if (scanResult.ScanException != null)
            {
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "WARNING: Report is incomplete because: {0}",
                                                                        scanResult.ScanException.Message));
            }
            if (scanResult.FileExceptions.Count > 0)
            {
                sw.WriteLine("WARNING: File errors were encountered during scan:");
                foreach (string fileName in scanResult.FileExceptions.Keys)
                {
                    Exception fileException = scanResult.FileExceptions[fileName];
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                            "\r\n{0}\t{1}",
                                                                            fileName, fileException.Message));
                    sw.WriteLine(fileException.StackTrace);
                }
            }

            string separator = new('#', 10);

            foreach (TSPlaylistFile playlist in playlists.Where(pl => !settings.FilterLoopingPlaylists || pl.IsValid))
            {
                StringBuilder summary = new();
                string title = playlist.Name;
                string discSize = string.Format(CultureInfo.InvariantCulture,
                                                                                                        "{0:N0}", BDROM.Size);

                TimeSpan playlistTotalLength =
                        new((long)(playlist.TotalLength * 10000000));

                string totalLength = string.Format(CultureInfo.InvariantCulture,
                                                                                                        "{0:D1}:{1:D2}:{2:D2}.{3:D3}",
                                                                                                        playlistTotalLength.Hours,
                                                                                                        playlistTotalLength.Minutes,
                                                                                                        playlistTotalLength.Seconds,
                                                                                                        playlistTotalLength.Milliseconds);

                string totalLengthShort = string.Format(CultureInfo.InvariantCulture,
                                                                                                        "{0:D1}:{1:D2}:{2:D2}",
                                                                                                        playlistTotalLength.Hours,
                                                                                                        playlistTotalLength.Minutes,
                                                                                                        playlistTotalLength.Seconds);

                string totalSize = string.Format(CultureInfo.InvariantCulture,
                                                                                                        "{0:N0}", playlist.TotalSize);

                string totalBitrate = string.Format(CultureInfo.InvariantCulture,
                                                                                                        "{0:F2}",
                                                                                                        Math.Round((double)playlist.TotalBitRate / 10000) / 100);

                TimeSpan playlistAngleLength = new((long)(playlist.TotalAngleLength * 10000000));

                string totalAngleLength = string.Format(CultureInfo.InvariantCulture,
                                                                                                        "{0:D1}:{1:D2}:{2:D2}.{3:D3}",
                                                                                                        playlistAngleLength.Hours,
                                                                                                        playlistAngleLength.Minutes,
                                                                                                        playlistAngleLength.Seconds,
                                                                                                        playlistAngleLength.Milliseconds);

                string totalAngleSize = string.Format(CultureInfo.InvariantCulture,
                                                                                                        "{0:N0}", playlist.TotalAngleSize);

                string totalAngleBitrate = string.Format(CultureInfo.InvariantCulture,
                                                                                                        "{0:F2}",
                                                                                                        Math.Round((double)playlist.TotalAngleBitRate / 10000) / 100);

                List<string> angleLengths = [];
                List<string> angleSizes = [];
                List<string> angleBitrates = [];
                List<string> angleTotalLengths = [];
                List<string> angleTotalSizes = [];
                List<string> angleTotalBitrates = [];
                if (playlist.AngleCount > 0)
                {
                    for (int angleIndex = 0; angleIndex < playlist.AngleCount; angleIndex++)
                    {
                        double angleLength = 0;
                        ulong angleSize = 0;
                        ulong angleTotalSize = 0;
                        if (angleIndex < playlist.AngleClips.Count &&
                                playlist.AngleClips[angleIndex] != null)
                        {
                            foreach (TSStreamClip clip in playlist.AngleClips[angleIndex].Values)
                            {
                                angleTotalSize += clip.PacketSize;
                                if (clip.AngleIndex == angleIndex + 1)
                                {
                                    angleSize += clip.PacketSize;
                                    angleLength += clip.Length;
                                }
                            }
                        }

                        angleSizes.Add(string.Format(CultureInfo.InvariantCulture, "{0:N0}", angleSize));

                        TimeSpan angleTimeSpan = new((long)(angleLength * 10000000));

                        angleLengths.Add(string.Format(CultureInfo.InvariantCulture,
                                                                                        "{0:D1}:{1:D2}:{2:D2}.{3:D3}",
                                                                                        angleTimeSpan.Hours,
                                                                                        angleTimeSpan.Minutes,
                                                                                        angleTimeSpan.Seconds,
                                                                                        angleTimeSpan.Milliseconds));

                        angleTotalSizes.Add(string.Format(CultureInfo.InvariantCulture, "{0:N0}", angleTotalSize));

                        angleTotalLengths.Add(totalLength);

                        double angleBitrate = 0;
                        if (angleLength > 0)
                        {
                            angleBitrate = Math.Round((double)(angleSize * 8) / angleLength / 10000) / 100;
                        }
                        angleBitrates.Add(string.Format(CultureInfo.InvariantCulture, "{0:F2}", angleBitrate));

                        double angleTotalBitrate2 = 0;
                        if (playlist.TotalLength > 0)
                        {
                            angleTotalBitrate2 = Math.Round((double)(angleTotalSize * 8) / playlist.TotalLength / 10000) / 100;
                        }
                        angleTotalBitrates.Add(string.Format(CultureInfo.InvariantCulture, "{0:F2}", angleTotalBitrate2));
                    }
                }

                string videoCodec = "";
                string videoBitrate = "";
                if (playlist.VideoStreams.Count > 0)
                {
                    TSStream videoStream = playlist.VideoStreams[0];
                    videoCodec = videoStream.CodecAltName;
                    videoBitrate = string.Format(CultureInfo.InvariantCulture, "{0:F2}", Math.Round((double)videoStream.BitRate / 10000) / 100);
                }

                StringBuilder audio1 = new();
                string languageCode1 = "";
                if (playlist.AudioStreams.Count > 0)
                {
                    TSAudioStream audioStream = playlist.AudioStreams[0];

                    languageCode1 = audioStream.LanguageCode;

                    audio1.Append(string.Format(CultureInfo.InvariantCulture, "{0} {1}", audioStream.CodecAltName, audioStream.ChannelDescription));

                    if (audioStream.BitRate > 0)
                    {
                        audio1.Append(string.Format(CultureInfo.InvariantCulture,
                                                                                " {0}Kbps",
                                                                                (int)Math.Round((double)audioStream.BitRate / 1000)));
                    }

                    if (audioStream.SampleRate > 0 &&
                            audioStream.BitDepth > 0)
                    {
                        audio1.Append(string.Format(CultureInfo.InvariantCulture,
                                                                                " ({0}kHz/{1}-bit)",
                                                                                (int)Math.Round((double)audioStream.SampleRate / 1000),
                                                                                audioStream.BitDepth));
                    }
                }

                StringBuilder audio2 = new();
                if (playlist.AudioStreams.Count > 1)
                {
                    for (int i = 1; i < playlist.AudioStreams.Count; i++)
                    {
                        TSAudioStream audioStream = playlist.AudioStreams[i];

                        if (audioStream.LanguageCode == languageCode1 &&
                                audioStream.StreamType != TSStreamType.AC3_PLUS_SECONDARY_AUDIO &&
                                audioStream.StreamType != TSStreamType.DTS_HD_SECONDARY_AUDIO &&
                                !(audioStream.StreamType == TSStreamType.AC3_AUDIO &&
                                    audioStream.ChannelCount == 2))
                        {
                            audio2.Append(string.Format(CultureInfo.InvariantCulture,
                                                                                            "{0} {1}",
                                                                                            audioStream.CodecAltName, audioStream.ChannelDescription));

                            if (audioStream.BitRate > 0)
                            {
                                audio2.Append(string.Format(CultureInfo.InvariantCulture,
                                        " {0}Kbps",
                                        (int)Math.Round((double)audioStream.BitRate / 1000)));
                            }

                            if (audioStream.SampleRate > 0 &&
                                    audioStream.BitDepth > 0)
                            {
                                audio2.Append(string.Format(CultureInfo.InvariantCulture,
                                                                                        " ({0}kHz/{1}-bit)",
                                                                                        (int)Math.Round((double)audioStream.SampleRate / 1000),
                                                                                        audioStream.BitDepth));
                            }
                            break;
                        }
                    }
                }

                sw.WriteLine(Environment.NewLine);
                sw.WriteLine("********************");
                sw.WriteLine("PLAYLIST: " + playlist.Name);
                sw.WriteLine("********************");
                sw.WriteLine(Environment.NewLine);
                sw.WriteLine("<--- BEGIN FORUMS PASTE --->");
                sw.WriteLine("[code]");

                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-64}{1,-8}{2,-8}{3,-16}{4,-16}{5,-8}{6,-8}{7,-42}{8}",
                                                                "",
                                                                "",
                                                                "",
                                                                "",
                                                                "",
                                                                "Total",
                                                                "Video",
                                                                "",
                                                                ""));

                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-64}{1,-8}{2,-8}{3,-16}{4,-16}{5,-8}{6,-8}{7,-42}{8}",
                                                                "Title",
                                                                "Codec",
                                                                "Length",
                                                                "Movie Size",
                                                                "Disc Size",
                                                                "Bitrate",
                                                                "Bitrate",
                                                                "Main Audio Track",
                                                                "Secondary Audio Track"));

                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-64}{1,-8}{2,-8}{3,-16}{4,-16}{5,-8}{6,-8}{7,-42}{8}",
                                                                "-----",
                                                                "------",
                                                                "-------",
                                                                "--------------",
                                                                "--------------",
                                                                "-------",
                                                                "-------",
                                                                "------------------",
                                                                "---------------------"));

                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-64}{1,-8}{2,-8}{3,-16}{4,-16}{5,-8}{6,-8}{7,-42}{8}",
                                                                title,
                                                                videoCodec,
                                                                totalLengthShort,
                                                                totalSize,
                                                                discSize,
                                                                totalBitrate,
                                                                videoBitrate,
                                                                audio1.ToString(),
                                                                audio2.ToString()));

                sw.WriteLine("[/code]");
                sw.WriteLine(Environment.NewLine);
                sw.WriteLine("[code]");

                if (settings.GroupByTime)
                {
                    sw.WriteLine($"{Environment.NewLine}{separator}Start group {playlistTotalLength.TotalMilliseconds}{separator}");
                }

                sw.WriteLine(Environment.NewLine);
                sw.WriteLine("DISC INFO:");
                sw.WriteLine(Environment.NewLine);

                if (!string.IsNullOrEmpty(BDROM.DiscTitle))
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-16}{1}", "Disc Title:", BDROM.DiscTitle));

                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-16}{1}", "Disc Label:", BDROM.VolumeLabel));

                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-16}{1:N0} bytes", "Disc Size:", BDROM.Size));

                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-16}{1}", "Protection:", protection));

                if (extraFeatures.Count > 0)
                {
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-16}{1}", "Extras:", string.Join(", ", [.. extraFeatures])));
                }
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-16}{1}", "BDInfo:", productVersion));

                sw.WriteLine(Environment.NewLine);
                sw.WriteLine("PLAYLIST REPORT:");
                sw.WriteLine(Environment.NewLine);
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-24}{1}", "Name:", title));

                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-24}{1} (h:m:s.ms)", "Length:", totalLength));

                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-24}{1:N0} bytes", "Size:", totalSize));

                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-24}{1} Mbps", "Total Bitrate:", totalBitrate));
                if (playlist.AngleCount > 0)
                {
                    for (int angleIndex = 0; angleIndex < playlist.AngleCount; angleIndex++)
                    {
                        sw.WriteLine(Environment.NewLine);
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "{0,-24}{1} (h:m:s.ms) / {2} (h:m:s.ms)",
                                                                        string.Format(CultureInfo.InvariantCulture, "Angle {0} Length:", angleIndex + 1),
                                                                        angleLengths[angleIndex], angleTotalLengths[angleIndex]));

                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "{0,-24}{1:N0} bytes / {2:N0} bytes",
                                                                        string.Format(CultureInfo.InvariantCulture, "Angle {0} Size:", angleIndex + 1),
                                                                        angleSizes[angleIndex], angleTotalSizes[angleIndex]));

                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "{0,-24}{1} Mbps / {2} Mbps",
                                                                        string.Format(CultureInfo.InvariantCulture, "Angle {0} Total Bitrate:", angleIndex + 1),
                                                                        angleBitrates[angleIndex], angleTotalBitrates[angleIndex], angleIndex));
                    }
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-24}{1} (h:m:s.ms)", "All Angles Length:", totalAngleLength));

                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-24}{1} bytes", "All Angles Size:", totalAngleSize));

                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-24}{1} Mbps", "All Angles Bitrate:", totalAngleBitrate));
                }

                if (!string.IsNullOrEmpty(BDROM.DiscTitle))
                    summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "Disc Title: {0}", BDROM.DiscTitle));

                summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                                 "Disc Label: {0}", BDROM.VolumeLabel));

                summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                                 "Disc Size: {0:N0} bytes", BDROM.Size));

                summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                                 "Protection: {0}", protection));

                summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                                 "Playlist: {0}", title));

                summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                                 "Size: {0:N0} bytes", totalSize));

                summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                                 "Length: {0}", totalLength));

                summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                                 "Total Bitrate: {0} Mbps", totalBitrate));

                if (playlist.HasHiddenTracks)
                {
                    sw.WriteLine("\r\n(*) Indicates included stream hidden by this playlist.");
                }

                if (playlist.VideoStreams.Count > 0)
                {
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine("VIDEO:");
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-24}{1,-20}{2,-16}",
                                                                    "Codec",
                                                                    "Bitrate",
                                                                    "Description"));
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-24}{1,-20}{2,-16}",
                                                                    "-----",
                                                                    "-------",
                                                                    "-----------"));

                    foreach (TSStream stream in playlist.SortedStreams)
                    {
                        if (!stream.IsVideoStream) continue;

                        string streamName = stream.CodecName;
                        if (stream.AngleIndex > 0)
                        {
                            streamName = string.Format(CultureInfo.InvariantCulture,
                                                                                    "{0} ({1})", streamName, stream.AngleIndex);
                        }

                        string streamBitrate = string.Format(CultureInfo.InvariantCulture,
                                                                                                "{0:D}",
                                                                                                (int)Math.Round((double)stream.BitRate / 1000));
                        if (stream.AngleIndex > 0)
                        {
                            streamBitrate = string.Format(CultureInfo.InvariantCulture,
                                                                                            "{0} ({1:D})",
                                                                                            streamBitrate,
                                                                                            (int)Math.Round((double)stream.ActiveBitRate / 1000));
                        }
                        streamBitrate = $"{streamBitrate} kbps";

                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "{0,-24}{1,-20}{2,-16}",
                                                                        (stream.IsHidden ? "* " : "") + streamName,
                                                                        streamBitrate,
                                                                        stream.Description));

                        summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                                        (stream.IsHidden ? "* " : "") + "Video: {0} / {1} / {2}",
                                                                        streamName,
                                                                        streamBitrate,
                                                                        stream.Description));
                    }
                }

                if (playlist.AudioStreams.Count > 0)
                {
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine("AUDIO:");
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-32}{1,-16}{2,-16}{3,-16}",
                                                                    "Codec",
                                                                    "Language",
                                                                    "Bitrate",
                                                                    "Description"));
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-32}{1,-16}{2,-16}{3,-16}",
                                                                    "-----",
                                                                    "--------",
                                                                    "-------",
                                                                    "-----------"));

                    foreach (TSStream stream in playlist.SortedStreams)
                    {
                        if (!stream.IsAudioStream) continue;

                        string streamBitrate = string.Format(CultureInfo.InvariantCulture,
                                                                                                "{0:D} kbps",
                                                                                                (int)Math.Round((double)stream.BitRate / 1000));

                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "{0,-32}{1,-16}{2,-16}{3,-16}",
                                                                        (stream.IsHidden ? "* " : "") + stream.CodecName,
                                                                        stream.LanguageName,
                                                                        streamBitrate,
                                                                        stream.Description));

                        summary.AppendLine(string.Format(
                                (stream.IsHidden ? "* " : "") + "Audio: {0} / {1} / {2}",
                                stream.LanguageName,
                                stream.CodecName,
                                stream.Description));
                    }
                }

                if (playlist.GraphicsStreams.Count > 0)
                {
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine("SUBTITLES:");
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-32}{1,-16}{2,-16}{3,-16}",
                                                                    "Codec",
                                                                    "Language",
                                                                    "Bitrate",
                                                                    "Description"));
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-32}{1,-16}{2,-16}{3,-16}",
                                                                    "-----",
                                                                    "--------",
                                                                    "-------",
                                                                    "-----------"));

                    foreach (TSStream stream in playlist.SortedStreams)
                    {
                        if (!stream.IsGraphicsStream) continue;

                        string streamBitrate = string.Format(CultureInfo.InvariantCulture,
                                                                                                 "{0:F3} kbps",
                                                                                                 (double)stream.BitRate / 1000);

                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "{0,-32}{1,-16}{2,-16}{3,-16}",
                                                                        (stream.IsHidden ? "* " : "") + stream.CodecName,
                                                                        stream.LanguageName,
                                                                        streamBitrate,
                                                                        stream.Description));

                        summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                                                         (stream.IsHidden ? "* " : "") + "Subtitle: {0} / {1}",
                                                                         stream.LanguageName,
                                                                         streamBitrate,
                                                                         stream.Description));
                    }
                }

                if (playlist.TextStreams.Count > 0)
                {
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine("TEXT:");
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-32}{1,-16}{2,-16}{3,-16}",
                                                                    "Codec",
                                                                    "Language",
                                                                    "Bitrate",
                                                                    "Description"));
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-32}{1,-16}{2,-16}{3,-16}",
                                                                    "-----",
                                                                    "--------",
                                                                    "-------",
                                                                    "-----------"));

                    foreach (TSStream stream in playlist.SortedStreams)
                    {
                        if (!stream.IsTextStream) continue;

                        string streamBitrate = string.Format(CultureInfo.InvariantCulture,
                                                                                                 "{0:F3} kbps",
                                                                                                 (double)stream.BitRate / 1000);

                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "{0,-32}{1,-16}{2,-16}{3,-16}",
                                                                        (stream.IsHidden ? "* " : "") + stream.CodecName,
                                                                        stream.LanguageName,
                                                                        streamBitrate,
                                                                        stream.Description));
                    }
                }

                sw.WriteLine(Environment.NewLine);
                sw.WriteLine("FILES:");
                sw.WriteLine(Environment.NewLine);
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-16}{1,-16}{2,-16}{3,-16}{4,-16}",
                                                                "Name",
                                                                "Time In",
                                                                "Length",
                                                                "Size",
                                                                "Total Bitrate"));
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-16}{1,-16}{2,-16}{3,-16}{4,-16}",
                                                                "----",
                                                                "-------",
                                                                "------",
                                                                "----",
                                                                "-------------"));

                foreach (TSStreamClip clip in playlist.StreamClips)
                {
                    string clipName = clip.DisplayName;

                    if (clip.AngleIndex > 0)
                    {
                        clipName = string.Format(CultureInfo.InvariantCulture,
                                                                            "{0} ({1})", clipName, clip.AngleIndex);
                    }

                    string clipSize = string.Format(CultureInfo.InvariantCulture,
                                                                                    "{0:N0}", clip.PacketSize);

                    TimeSpan clipInSpan =
                            new((long)(clip.RelativeTimeIn * 10000000));
                    TimeSpan clipOutSpan =
                            new((long)(clip.RelativeTimeOut * 10000000));
                    TimeSpan clipLengthSpan =
                            new((long)(clip.Length * 10000000));

                    string clipTimeIn = string.Format(CultureInfo.InvariantCulture,
                                                                                            "{0:D1}:{1:D2}:{2:D2}.{3:D3}",
                                                                                            clipInSpan.Hours,
                                                                                            clipInSpan.Minutes,
                                                                                            clipInSpan.Seconds,
                                                                                            clipInSpan.Milliseconds);
                    string clipLength = string.Format(CultureInfo.InvariantCulture,
                                                                                        "{0:D1}:{1:D2}:{2:D2}.{3:D3}",
                                                                                        clipLengthSpan.Hours,
                                                                                        clipLengthSpan.Minutes,
                                                                                        clipLengthSpan.Seconds,
                                                                                        clipLengthSpan.Milliseconds);

                    string clipBitrate = Math.Round(
                            (double)clip.PacketBitRate / 1000).ToString("N0", CultureInfo.InvariantCulture);

                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-16}{1,-16}{2,-16}{3,-16}{4,-16}",
                                                                    clipName,
                                                                    clipTimeIn,
                                                                    clipLength,
                                                                    clipSize,
                                                                    clipBitrate));
                }

                if (settings.GroupByTime)
                {
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine(separator + "End group" + separator);
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine(Environment.NewLine);
                }

                sw.WriteLine(Environment.NewLine);
                sw.WriteLine("CHAPTERS:");
                sw.WriteLine(Environment.NewLine);
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-16}{1,-16}{2,-16}{3,-16}{4,-16}{5,-16}{6,-16}{7,-16}{8,-16}{9,-16}{10,-16}{11,-16}{12,-16}",
                                                                "Number",
                                                                "Time In",
                                                                "Length",
                                                                "Avg Video Rate",
                                                                "Max 1-Sec Rate",
                                                                "Max 1-Sec Time",
                                                                "Max 5-Sec Rate",
                                                                "Max 5-Sec Time",
                                                                "Max 10Sec Rate",
                                                                "Max 10Sec Time",
                                                                "Avg Frame Size",
                                                                "Max Frame Size",
                                                                "Max Frame Time"));
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                "{0,-16}{1,-16}{2,-16}{3,-16}{4,-16}{5,-16}{6,-16}{7,-16}{8,-16}{9,-16}{10,-16}{11,-16}{12,-16}",
                                                                "------",
                                                                "-------",
                                                                "------",
                                                                "--------------",
                                                                "--------------",
                                                                "--------------",
                                                                "--------------",
                                                                "--------------",
                                                                "--------------",
                                                                "--------------",
                                                                "--------------",
                                                                "--------------",
                                                                "--------------"));

                Queue<double> window1Bits = new();
                Queue<double> window1Seconds = new();
                double window1BitsSum = 0;
                double window1SecondsSum = 0;
                double window1PeakBitrate = 0;
                double window1PeakLocation = 0;

                Queue<double> window5Bits = new();
                Queue<double> window5Seconds = new();
                double window5BitsSum = 0;
                double window5SecondsSum = 0;
                double window5PeakBitrate = 0;
                double window5PeakLocation = 0;

                Queue<double> window10Bits = new();
                Queue<double> window10Seconds = new();
                double window10BitsSum = 0;
                double window10SecondsSum = 0;
                double window10PeakBitrate = 0;
                double window10PeakLocation = 0;

                double chapterPosition = 0;
                double chapterBits = 0;
                long chapterFrameCount = 0;
                double chapterSeconds = 0;
                double chapterMaxFrameSize = 0;
                double chapterMaxFrameLocation = 0;

                ushort diagPID = playlist.VideoStreams.FirstOrDefault()?.PID ?? 0;

                int chapterIndex = 0;
                int clipIndex = 0;
                int diagIndex = 0;

                while (chapterIndex < playlist.Chapters.Count)
                {
                    TSStreamClip clip = null;
                    TSStreamFile file = null;

                    if (clipIndex < playlist.StreamClips.Count)
                    {
                        clip = playlist.StreamClips[clipIndex];
                        file = clip.StreamFile;
                    }

                    double chapterStart = playlist.Chapters[chapterIndex];
                    double chapterEnd;
                    if (chapterIndex < playlist.Chapters.Count - 1)
                    {
                        chapterEnd = playlist.Chapters[chapterIndex + 1];
                    }
                    else
                    {
                        chapterEnd = playlist.TotalLength;
                    }
                    double chapterLength = chapterEnd - chapterStart;

                    List<TSStreamDiagnostics> diagList = null;

                    if (clip != null &&
                            clip.AngleIndex == 0 &&
                            file != null &&
                            file.StreamDiagnostics.ContainsKey(diagPID))
                    {
                        diagList = file.StreamDiagnostics[diagPID];

                        while (diagIndex < diagList.Count &&
                                chapterPosition < chapterEnd)
                        {
                            TSStreamDiagnostics diag = diagList[diagIndex++];

                            if (diag.Marker < clip.TimeIn) continue;

                            chapterPosition =
                                    diag.Marker -
                                    clip.TimeIn +
                                    clip.RelativeTimeIn;

                            double seconds = diag.Interval;
                            double bits = diag.Bytes * 8.0;

                            chapterBits += bits;
                            chapterSeconds += seconds;

                            if (diag.Tag != null)
                            {
                                chapterFrameCount++;
                            }

                            window1SecondsSum += seconds;
                            window1Seconds.Enqueue(seconds);
                            window1BitsSum += bits;
                            window1Bits.Enqueue(bits);

                            window5SecondsSum += diag.Interval;
                            window5Seconds.Enqueue(diag.Interval);
                            window5BitsSum += bits;
                            window5Bits.Enqueue(bits);

                            window10SecondsSum += seconds;
                            window10Seconds.Enqueue(seconds);
                            window10BitsSum += bits;
                            window10Bits.Enqueue(bits);

                            if (bits > chapterMaxFrameSize * 8)
                            {
                                chapterMaxFrameSize = bits / 8;
                                chapterMaxFrameLocation = chapterPosition;
                            }
                            if (window1SecondsSum > 1.0)
                            {
                                double bitrate = window1BitsSum / window1SecondsSum;
                                if (bitrate > window1PeakBitrate &&
                                        chapterPosition - window1SecondsSum > 0)
                                {
                                    window1PeakBitrate = bitrate;
                                    window1PeakLocation = chapterPosition - window1SecondsSum;
                                }
                                window1BitsSum -= window1Bits.Dequeue();
                                window1SecondsSum -= window1Seconds.Dequeue();
                            }
                            if (window5SecondsSum > 5.0)
                            {
                                double bitrate = window5BitsSum / window5SecondsSum;
                                if (bitrate > window5PeakBitrate &&
                                        chapterPosition - window5SecondsSum > 0)
                                {
                                    window5PeakBitrate = bitrate;
                                    window5PeakLocation = chapterPosition - window5SecondsSum;
                                    if (window5PeakLocation < 0)
                                    {
                                        window5PeakLocation = 0;
                                        window5PeakLocation = 0;
                                    }
                                }
                                window5BitsSum -= window5Bits.Dequeue();
                                window5SecondsSum -= window5Seconds.Dequeue();
                            }
                            if (window10SecondsSum > 10.0)
                            {
                                double bitrate = window10BitsSum / window10SecondsSum;
                                if (bitrate > window10PeakBitrate &&
                                        chapterPosition - window10SecondsSum > 0)
                                {
                                    window10PeakBitrate = bitrate;
                                    window10PeakLocation = chapterPosition - window10SecondsSum;
                                }
                                window10BitsSum -= window10Bits.Dequeue();
                                window10SecondsSum -= window10Seconds.Dequeue();
                            }
                        }
                    }
                    if (diagList == null ||
                            diagIndex == diagList.Count)
                    {
                        if (clipIndex < playlist.StreamClips.Count)
                        {
                            clipIndex++; diagIndex = 0;
                        }
                        else
                        {
                            chapterPosition = chapterEnd;
                        }
                    }
                    if (chapterPosition >= chapterEnd)
                    {
                        ++chapterIndex;

                        TimeSpan window1PeakSpan = new((long)(window1PeakLocation * 10000000));
                        TimeSpan window5PeakSpan = new((long)(window5PeakLocation * 10000000));
                        TimeSpan window10PeakSpan = new((long)(window10PeakLocation * 10000000));
                        TimeSpan chapterMaxFrameSpan = new((long)(chapterMaxFrameLocation * 10000000));
                        TimeSpan chapterStartSpan = new((long)(chapterStart * 10000000));
                        TimeSpan chapterEndSpan = new((long)(chapterEnd * 10000000));
                        TimeSpan chapterLengthSpan = new((long)(chapterLength * 10000000));

                        double chapterBitrate = 0;
                        if (chapterLength > 0)
                        {
                            chapterBitrate = chapterBits / chapterLength;
                        }
                        double chapterAvgFrameSize = 0;
                        if (chapterFrameCount > 0)
                        {
                            chapterAvgFrameSize = chapterBits / chapterFrameCount / 8;
                        }

                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                        "{0,-16}{1,-16}{2,-16}{3,-16}{4,-16}{5,-16}{6,-16}{7,-16}{8,-16}{9,-16}{10,-16}{11,-16}{12,-16}",
                                                                        chapterIndex,
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:D1}:{1:D2}:{2:D2}.{3:D3}", chapterStartSpan.Hours, chapterStartSpan.Minutes, chapterStartSpan.Seconds, chapterStartSpan.Milliseconds),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:D1}:{1:D2}:{2:D2}.{3:D3}", chapterLengthSpan.Hours, chapterLengthSpan.Minutes, chapterLengthSpan.Seconds, chapterLengthSpan.Milliseconds),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:N0} kbps", Math.Round(chapterBitrate / 1000)),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:N0} kbps", Math.Round(window1PeakBitrate / 1000)),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}.{3:D3}", window1PeakSpan.Hours, window1PeakSpan.Minutes, window1PeakSpan.Seconds, window1PeakSpan.Milliseconds),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:N0} kbps", Math.Round(window5PeakBitrate / 1000)),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}.{3:D3}", window5PeakSpan.Hours, window5PeakSpan.Minutes, window5PeakSpan.Seconds, window5PeakSpan.Milliseconds),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:N0} kbps", Math.Round(window10PeakBitrate / 1000)),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}.{3:D3}", window10PeakSpan.Hours, window10PeakSpan.Minutes, window10PeakSpan.Seconds, window10PeakSpan.Milliseconds),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:N0} bytes", chapterAvgFrameSize),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:N0} bytes", chapterMaxFrameSize),
                                                                        string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}.{3:D3}", chapterMaxFrameSpan.Hours, chapterMaxFrameSpan.Minutes, chapterMaxFrameSpan.Seconds, chapterMaxFrameSpan.Milliseconds)));

                        window1Bits = new Queue<double>();
                        window1Seconds = new Queue<double>();
                        window1BitsSum = 0;
                        window1SecondsSum = 0;
                        window1PeakBitrate = 0;
                        window1PeakLocation = 0;

                        window5Bits = new Queue<double>();
                        window5Seconds = new Queue<double>();
                        window5BitsSum = 0;
                        window5SecondsSum = 0;
                        window5PeakBitrate = 0;
                        window5PeakLocation = 0;

                        window10Bits = new Queue<double>();
                        window10Seconds = new Queue<double>();
                        window10BitsSum = 0;
                        window10SecondsSum = 0;
                        window10PeakBitrate = 0;
                        window10PeakLocation = 0;

                        chapterBits = 0;
                        chapterSeconds = 0;
                        chapterFrameCount = 0;
                        chapterMaxFrameSize = 0;
                        chapterMaxFrameLocation = 0;
                    }
                }

                if (settings.GenerateStreamDiagnostics)
                {
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine("STREAM DIAGNOSTICS:");
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-16}{1,-16}{2,-16}{3,-16}{4,-24}{5,-24}{6,-24}{7,-16}{8,-16}",
                                                                    "File",
                                                                    "PID",
                                                                    "Type",
                                                                    "Codec",
                                                                    "Language",
                                                                    "Seconds",
                                                                    "Bitrate",
                                                                    "Bytes",
                                                                    "Packets"));
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                    "{0,-16}{1,-16}{2,-16}{3,-16}{4,-24}{5,-24}{6,-24}{7,-16}{8,-16}",
                                                                    "----",
                                                                    "---",
                                                                    "----",
                                                                    "-----",
                                                                    "--------",
                                                                    "--------------",
                                                                    "--------------",
                                                                    "-------------",
                                                                    "-----",
                                                                    "-------"));

                    Dictionary<string, TSStreamClip> reportedClips = [];
                    foreach (TSStreamClip clip in playlist.StreamClips)
                    {
                        if (clip.StreamFile == null) continue;
                        if (reportedClips.ContainsKey(clip.Name)) continue;
                        reportedClips[clip.Name] = clip;

                        string clipName = clip.DisplayName;
                        if (clip.AngleIndex > 0)
                        {
                            clipName = string.Format(CultureInfo.InvariantCulture, "{0} ({1})", clipName, clip.AngleIndex);
                        }
                        foreach (TSStream clipStream in clip.StreamFile.Streams.Values)
                        {
                            if (!playlist.Streams.ContainsKey(clipStream.PID)) continue;

                            TSStream playlistStream =
                                    playlist.Streams[clipStream.PID];

                            string clipBitRate = "0";
                            string clipSeconds = "0";

                            if (clip.StreamFile.Length > 0)
                            {
                                clipSeconds =
                                        clip.StreamFile.Length.ToString("F3", CultureInfo.InvariantCulture);
                                clipBitRate = Math.Round(
                                         (double)clipStream.PayloadBytes * 8 /
                                         clip.StreamFile.Length / 1000).ToString("N0", CultureInfo.InvariantCulture);
                            }
                            string language = "";
                            if (!string.IsNullOrEmpty(playlistStream.LanguageCode))
                            {
                                language = string.Format(CultureInfo.InvariantCulture,
                                        "{0} ({1})", playlistStream.LanguageCode, playlistStream.LanguageName);
                            }

                            sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                                                            "{0,-16}{1,-16}{2,-16}{3,-16}{4,-24}{5,-24}{6,-24}{7,-16}{8,-16}",
                                                                            clipName,
                                                                            string.Format(CultureInfo.InvariantCulture, "{0} (0x{1:X})", clipStream.PID, clipStream.PID),
                                                                            string.Format(CultureInfo.InvariantCulture, "0x{0:X2}", (byte)clipStream.StreamType),
                                                                            clipStream.CodecShortName,
                                                                            language,
                                                                            clipSeconds,
                                                                            clipBitRate,
                                                                            clipStream.PayloadBytes.ToString("N0", CultureInfo.InvariantCulture),
                                                                            clipStream.PacketCount.ToString("N0", CultureInfo.InvariantCulture)));
                        }
                    }
                }

                sw.WriteLine(Environment.NewLine);
                sw.WriteLine("[/code]");
                sw.WriteLine("<---- END FORUMS PASTE ---->");
                sw.WriteLine(Environment.NewLine);

                if (settings.GenerateTextSummary)
                {
                    sw.WriteLine("QUICK SUMMARY:");
                    sw.WriteLine(Environment.NewLine);
                    sw.WriteLine(summary.ToString());
                    sw.WriteLine(Environment.NewLine);
                }

                sw.WriteLine(Environment.NewLine);

                File.AppendAllLines(debugLogPath, [Environment.NewLine, "appending report to tmp", Environment.NewLine]);
                GC.Collect();
            }
        }
    }
}
