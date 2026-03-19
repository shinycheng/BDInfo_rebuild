using BDCommon;
using CommandLine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BDInfo
{
    internal class Program
    {
        private static readonly string ProductVersion = "0.8.0.0";
        private static readonly string BDMV = "BDMV";

        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<CmdOptions>(args)
                .WithParsed(opts => Exec(opts));
        }

        private static void Exec(CmdOptions opts)
        {
            string error = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"error_{Path.GetFileName(opts.Path)}.log");
            string debug = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"debug_{Path.GetFileName(opts.Path)}.log");
            BDSettings bdinfoSettings = new BDSettings(opts);

            try
            {
                if (!opts.Path.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                {
                    var subItems = Directory.GetDirectories(opts.Path, BDMV, SearchOption.AllDirectories);
                    bool isIsoLevel = false;

                    if (subItems.Length == 0)
                    {
                        var di = new DirectoryInfo(opts.Path);
                        var files = di.GetFiles("*.*", SearchOption.AllDirectories);
                        subItems = files.Where(s => s.FullName.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)).Select(s => s.FullName).ToArray();
                        isIsoLevel = subItems.LongLength > 0;
                    }

                    if (subItems.Length > 1 || isIsoLevel)
                    {
                        var oldOpt = Cloner.Clone(opts);
                        var sortedItems = subItems.OrderBy(s => s).ToArray();

                        // Thread-safe collection: discPath → reportPath
                        var reportMap = new ConcurrentDictionary<string, string>();

                        ParallelOptions parallelOptions = new()
                        {
                            MaxDegreeOfParallelism = bdinfoSettings.MaxThreads
                        };

                        Parallel.ForEach(sortedItems, parallelOptions, subDir =>
                        {
                            string reportPath = ProcessSingleDisc(subDir, oldOpt, isIsoLevel);
                            if (reportPath != null)
                            {
                                reportMap[subDir] = reportPath;
                            }
                        });

                        // Merge reports in original sorted order (serial)
                        var reports = sortedItems
                            .Where(s => reportMap.ContainsKey(s))
                            .Select(s => reportMap[s])
                            .ToList();

                        if (reports.Count > 0)
                        {
                            var bigReport = oldOpt.ReportFileName;
                            if (reports.Count == 1)
                            {
                                File.AppendAllLines(debug, [Environment.NewLine, $"move file from {reports[0]} to {bigReport}", Environment.NewLine]);
                                File.Move(reports[0], bigReport);
                                TryDeleteFile(debug);
                                return;
                            }

                            foreach (var report in reports)
                            {
                                File.AppendAllLines(debug, [Environment.NewLine, "appending big reports", Environment.NewLine]);
                                File.AppendAllLines(bigReport, File.ReadAllLines(report));
                                File.AppendAllLines(bigReport, Enumerable.Repeat(Environment.NewLine, 5));

                                File.AppendAllLines(debug, [Environment.NewLine, "delete report file after appending to big report", Environment.NewLine]);
                                File.Delete(report);
                            }
                        }

                        TryDeleteFile(debug);
                        return;
                    }
                }

                BDROM singleBdrom = BDROMInitializer.InitBDROM(opts.Path, bdinfoSettings, error);
                BDROMScanner.ScanBDROM(singleBdrom, bdinfoSettings, ProductVersion, error, debug);
                TryDeleteFile(debug);
            }
            catch (Exception ex)
            {
                var color = Console.ForegroundColor;
                Console.Error.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"{opts.Path} ::: {ex.Message}");

                Console.ForegroundColor = color;

                try
                {
                    File.AppendAllText(error, $"{ex}{Environment.NewLine}{Environment.NewLine}");
                }
                catch
                {
                    // kills error
                }

                Environment.Exit(1);
            }
        }

        // Process a single disc: init → scan → report. No shared mutable state.
        private static string ProcessSingleDisc(string subDir, CmdOptions originalOpts, bool isIsoLevel)
        {
            // Clone opts so each disc gets its own independent copy
            var discOpts = Cloner.Clone(originalOpts);
            discOpts.Path = isIsoLevel ? subDir : Path.GetDirectoryName(subDir);

            // Per-disc error/debug log paths
            string discError = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"error_{Path.GetFileName(discOpts.Path)}.log");
            string discDebug = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"debug_{Path.GetFileName(discOpts.Path)}.log");

            // Per-disc settings (reads from its own discOpts copy)
            BDSettings discSettings = new BDSettings(discOpts);

            string reportPath = null;
            if (!string.IsNullOrWhiteSpace(discOpts.ReportFileName))
            {
                var parent = Path.GetDirectoryName(discOpts.Path);
                discOpts.ReportFileName = isIsoLevel
                    ? Path.Combine(parent, Path.GetFileNameWithoutExtension(discOpts.Path))
                    : Path.Combine(parent, Path.GetFileName(discOpts.Path)) + "." + Path.GetExtension(discOpts.ReportFileName).TrimStart('.');
                reportPath = discOpts.ReportFileName;
            }

            BDROM bdrom = BDROMInitializer.InitBDROM(discOpts.Path, discSettings, discError);
            BDROMScanner.ScanBDROM(bdrom, discSettings, ProductVersion, discError, discDebug);
            TryDeleteFile(discDebug);

            return reportPath;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // best-effort cleanup, ignore errors
            }
        }
    }
}
