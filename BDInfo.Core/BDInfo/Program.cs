using BDCommon;
using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
                        List<string> reports = [];

                        foreach (var subDir in subItems.OrderBy(s => s))
                        {
                            opts.Path = isIsoLevel ? subDir : Path.GetDirectoryName(subDir);
                            if (!string.IsNullOrWhiteSpace(opts.ReportFileName))
                            {
                                var parent = Path.GetDirectoryName(opts.Path);
                                opts.ReportFileName = isIsoLevel ? Path.Combine(parent, Path.GetFileNameWithoutExtension(opts.Path)) : Path.Combine(parent, Path.GetFileName(opts.Path)) + "." + Path.GetExtension(opts.ReportFileName).TrimStart('.');
                                reports.Add(opts.ReportFileName);
                            }

                            BDROM bdrom = BDROMInitializer.InitBDROM(opts.Path, bdinfoSettings, error);
                            BDROMScanner.ScanBDROM(bdrom, bdinfoSettings, ProductVersion, error, debug);
                        }

                        if (reports.Count > 0)
                        {
                            var bigReport = oldOpt.ReportFileName;
                            if (reports.Count == 1)
                            {
                                File.AppendAllLines(debug, [Environment.NewLine, $"move file from {reports[0]} to {bigReport}", Environment.NewLine]);
                                File.Move(reports[0], bigReport);
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

                        return;
                    }
                }

                BDROM singleBdrom = BDROMInitializer.InitBDROM(opts.Path, bdinfoSettings, error);
                BDROMScanner.ScanBDROM(singleBdrom, bdinfoSettings, ProductVersion, error, debug);
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
    }
}
