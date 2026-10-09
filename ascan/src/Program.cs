using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ascan
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            var options = Cli.Parse(args);
            if (options.Error != null)
            {
                Console.Error.WriteLine("Chyba: " + options.Error);
                Console.Error.WriteLine("Nápověda: ascan -h");
                return ExitCodes.Usage;
            }

            var config = new Config(Config.DefaultPath());
            try
            {
                switch (options.Command)
                {
                    case Command.Help:
                        Console.WriteLine(Cli.HelpText);
                        return ExitCodes.Ok;
                    case Command.List:
                        return List(config);
                    case Command.Scan:
                        return Scan(config, options);
                    default:
                        return Select(config);
                }
            }
            catch (WiaUnavailableException e)
            {
                Console.Error.WriteLine("Chyba: " + e.Message);
                return ExitCodes.NoScanner;
            }
            catch (COMException e)
            {
                Console.Error.WriteLine("Chyba skeneru: " + Cli.DescribeWiaError(e.HResult) + ".");
                return ExitCodes.ScanFailed;
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is System.IO.IOException)
            {
                Console.Error.WriteLine("Chyba: " + e.Message);
                return ExitCodes.ScanFailed;
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

        static int Scan(Config config, Options options)
        {
            string path = Cli.ResolveOutputPath(options.OutputPath, out string formatId);
            if (path == null)
            {
                Console.Error.WriteLine("Chyba: nepodporovaný formát souboru (použijte .jpg, .png, .bmp, .tif nebo .gif).");
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
                    Console.Error.WriteLine("Chyba: nastavený skener \"" + (config.DeviceName ?? config.DeviceId) + "\" není dostupný.");
                    Console.Error.WriteLine("Jiný skener vyberete příkazem: ascan");
                    return ExitCodes.NoScanner;
                }
                scanner = scanners[index];
            }

            Console.WriteLine("Skenuji: " + scanner.Name + " (" + options.Dpi + " DPI, " + Cli.Describe(options.Color) + ")...");
            Wia.Scan(scanner.Id, path, formatId, options.Dpi, options.Color, w => Console.Error.WriteLine("Upozornění: " + w));
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
            Console.Error.WriteLine("Nebyl nalezen žádný skener. Zkontrolujte, že je skener zapnutý, připojený a má nainstalovaný ovladač (WIA).");
            return ExitCodes.NoScanner;
        }
    }
}
