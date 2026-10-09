using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Ascan
{
    static class Program
    {
        static readonly List<string> Errors = new List<string>();

        [STAThread]
        static int Main(string[] args)
        {
            var options = Cli.Parse(args);
            string resultPath = options.OutputPath != null ? Cli.ResultPathFor(options.OutputPath) : null;
            string scannedPath = null;
            int code = Run(options, ref scannedPath);
            if (resultPath != null) WriteResult(resultPath, code == ExitCodes.Ok ? scannedPath : string.Join("\r\n", Errors));
            return code;
        }

        static int Run(Options options, ref string scannedPath)
        {
            if (options.Error != null)
            {
                Error("Chyba: " + options.Error);
                Console.Error.WriteLine("Nápověda: ascan -h");
                return ExitCodes.Usage;
            }

            try
            {
                var config = new Config(Config.DefaultPath());
                switch (options.Command)
                {
                    case Command.Help:
                        Console.WriteLine(Cli.HelpText);
                        return ExitCodes.Ok;
                    case Command.List:
                        return List(config);
                    case Command.Scan:
                        return Scan(config, options, ref scannedPath);
                    default:
                        return Select(config);
                }
            }
            catch (WiaUnavailableException e)
            {
                Error("Chyba: " + e.Message);
                return ExitCodes.NoScanner;
            }
            catch (COMException e)
            {
                Error("Chyba skeneru: " + Cli.DescribeWiaError(e.HResult) + ".");
                return ExitCodes.ScanFailed;
            }
            catch (Exception e)
            {
                Error("Chyba: " + e.Message);
                return ExitCodes.ScanFailed;
            }
        }

        static void Error(string message)
        {
            Errors.Add(message);
            Console.Error.WriteLine(message);
        }

        static void WriteResult(string path, string content)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, (content ?? "") + "\r\n", new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Chyba: nelze zapsat " + path + ": " + e.Message);
            }
        }

        static int List(Config config)
        {
            var scanners = Wia.ListScanners();
            if (scanners.Count == 0) return NoScanners();
            PrintScanners(scanners, config);
            return ExitCodes.Ok;
        }

        static int Select(Config config)
        {
            var scanners = Wia.ListScanners();
            if (scanners.Count == 0) return NoScanners();

            int defaultIndex = PrintScanners(scanners, config);
            while (true)
            {
                Console.Write("Vyberte číslo skeneru [Enter = " + (defaultIndex + 1) + ", W = výběr ve Windows, Q = konec]: ");
                string line = Console.ReadLine();
                if (line == null) return ExitCodes.Ok;

                var choice = Cli.ParseChoice(line, scanners.Count, defaultIndex);
                switch (choice.Kind)
                {
                    case ChoiceKind.Index:
                        return Save(config, scanners[choice.Index]);
                    case ChoiceKind.WindowsDialog:
                        var picked = Wia.SelectWithWindowsDialog();
                        if (picked != null) return Save(config, picked);
                        Console.WriteLine("Výběr zrušen.");
                        break;
                    case ChoiceKind.Quit:
                        Console.WriteLine("Nastavení beze změny.");
                        return ExitCodes.Ok;
                    default:
                        Console.WriteLine("Neplatná volba.");
                        break;
                }
            }
        }

        static int Scan(Config config, Options options, ref string scannedPath)
        {
            string path = Cli.ResolveOutputPath(options.OutputPath, out string formatId);
            if (path == null)
            {
                Error("Chyba: nepodporovaný formát souboru (použijte .pdf, .jpg, .png, .bmp, .tif nebo .gif).");
                return ExitCodes.Usage;
            }

            var scanners = Wia.ListScanners();
            if (scanners.Count == 0) return NoScanners();

            ScannerInfo scanner;
            if (string.IsNullOrEmpty(config.DeviceId))
            {
                scanner = scanners[0];
                Console.WriteLine("Skener není nastaven, používám výchozí skener Windows: " + scanner.Name);
            }
            else
            {
                int index = Cli.IndexOf(scanners.Select(s => s.Id).ToList(), config.DeviceId);
                if (index < 0)
                {
                    Error("Chyba: nastavený skener \"" + (config.DeviceName ?? config.DeviceId) + "\" není dostupný.");
                    Error("Jiný skener vyberete příkazem: ascan");
                    return ExitCodes.NoScanner;
                }
                scanner = scanners[index];
            }

            Console.WriteLine("Skenuji: " + scanner.Name + " (" + options.Dpi + " DPI, " + Cli.Describe(options.Color) + ")...");
            Wia.Scan(scanner.Id, path, formatId, options.Dpi, options.Color, w => Console.Error.WriteLine("Upozornění: " + w));
            scannedPath = path;
            Console.WriteLine("Uloženo: " + path);
            return ExitCodes.Ok;
        }

        /// <summary>Prints the numbered list and returns the index offered as default.</summary>
        static int PrintScanners(List<ScannerInfo> scanners, Config config)
        {
            int configured = Cli.IndexOf(scanners.Select(s => s.Id).ToList(), config.DeviceId);
            Console.WriteLine("Dostupné skenery:");
            for (int i = 0; i < scanners.Count; i++)
            {
                string mark = i == configured ? "   (nastavený)"
                    : configured < 0 && i == 0 ? "   (výchozí ve Windows)"
                    : "";
                Console.WriteLine("  " + (i + 1) + ") " + scanners[i].Name + mark);
            }
            if (!string.IsNullOrEmpty(config.DeviceId) && configured < 0)
                Console.WriteLine("Nastavený skener \"" + (config.DeviceName ?? config.DeviceId) + "\" není připojen.");
            else if (configured < 0)
                Console.WriteLine("Žádný skener zatím není nastaven.");
            return configured >= 0 ? configured : 0;
        }

        static int Save(Config config, ScannerInfo scanner)
        {
            config.Save(scanner.Id, scanner.Name);
            Console.WriteLine("Nastaven skener: " + scanner.Name);
            return ExitCodes.Ok;
        }

        static int NoScanners()
        {
            Error("Nebyl nalezen žádný skener. Zkontrolujte, že je skener zapnutý, připojený a má nainstalovaný ovladač (WIA).");
            return ExitCodes.NoScanner;
        }
    }
}
