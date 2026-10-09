using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Ascan
{
    enum Command { Select, List, Scan, Help }

    enum ColorMode { Color = 1, Gray = 2, BlackWhite = 4 }

    static class ExitCodes
    {
        public const int Ok = 0;
        public const int Usage = 1;
        public const int NoScanner = 2;
        public const int ScanFailed = 3;
    }

    sealed class Options
    {
        public Command Command = Command.Select;
        public string OutputPath;
        public int Dpi = 300;
        public ColorMode Color = ColorMode.Color;
        public string Error;
    }

    enum ChoiceKind { Index, WindowsDialog, Quit, Invalid }

    readonly struct Choice
    {
        public readonly ChoiceKind Kind;
        public readonly int Index;
        public Choice(ChoiceKind kind, int index = -1) { Kind = kind; Index = index; }
    }

    static class ImageFormats
    {
        public const string Bmp = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}";
        public const string Png = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";
        public const string Gif = "{B96B3CB0-0728-11D3-9D7B-0000F81EF32E}";
        public const string Jpeg = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
        public const string Tiff = "{B96B3CB1-0728-11D3-9D7B-0000F81EF32E}";
        public const string Pdf = "PDF";

        static readonly Dictionary<string, string> ByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".bmp"] = Bmp,
            [".png"] = Png,
            [".gif"] = Gif,
            [".jpg"] = Jpeg,
            [".jpeg"] = Jpeg,
            [".tif"] = Tiff,
            [".tiff"] = Tiff,
            [".pdf"] = Pdf,
        };

        public static string ForPath(string path) =>
            ByExtension.TryGetValue(Path.GetExtension(path), out var id) ? id : null;
    }

    static class Cli
    {
        public const string HelpText =
@"ascan - skenování z příkazového řádku (Windows, WIA)

Použití:
  ascan                  výběr skeneru ze seznamu (uloží se jako nastavený)
  ascan <soubor>         naskenuje bez dalších dotazů stránku ze skeneru do souboru;
                         do stejné složky zapíše result.txt (cesta k souboru
                         nebo chybové hlášky)
  ascan -l               vypíše dostupné skenery
  ascan -h               tato nápověda

Volby skenování:
  -d, --dpi <N>          rozlišení v DPI (výchozí 300)
  -c, --barva <režim>    barva | seda | cb   (výchozí barva)

Formát se určí podle přípony: .pdf .jpg .png .bmp .tif .gif
(bez přípony se použije .jpg).

Pokud není nastaven žádný skener, použije se výchozí skener Windows
(první skener, který Windows hlásí).

Příklady:
  ascan
  ascan doklad.pdf
  ascan C:\Skeny\smlouva.png -d 200 -c seda";

        public static Options Parse(string[] args)
        {
            var o = new Options();
            bool modeSet = false;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a.ToLowerInvariant())
                {
                    case "-h": case "--help": case "/?": case "-?": case "/h":
                        o.Command = Command.Help;
                        return o;
                    case "-l": case "--list": case "/l":
                        o.Command = Command.List;
                        modeSet = true;
                        break;
                    case "-s": case "--select": case "/s":
                        o.Command = Command.Select;
                        modeSet = true;
                        break;
                    case "-d": case "--dpi": case "/d":
                        if (++i >= args.Length || !int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out o.Dpi) || o.Dpi < 50 || o.Dpi > 4800)
                            return Fail(o, "volba " + a + " vyžaduje rozlišení 50-4800 DPI.");
                        break;
                    case "-c": case "--barva": case "/c":
                        if (++i >= args.Length) return Fail(o, "volba " + a + " vyžaduje režim (barva, seda, cb).");
                        var mode = ParseColor(args[i]);
                        if (mode == null) return Fail(o, "neznámý barevný režim '" + args[i] + "' (barva, seda, cb).");
                        o.Color = mode.Value;
                        break;
                    default:
                        if (a.Length > 1 && (a[0] == '-' || (a[0] == '/' && a.Length <= 3)))
                            return Fail(o, "neznámá volba '" + a + "'.");
                        if (o.OutputPath != null) return Fail(o, "zadejte jen jeden výstupní soubor.");
                        o.OutputPath = a;
                        break;
                }
            }
            if (o.OutputPath != null)
            {
                if (modeSet) return Fail(o, "výstupní soubor nelze kombinovat s -l / -s.");
                o.Command = Command.Scan;
            }
            return o;
        }

        static Options Fail(Options o, string message)
        {
            o.Error = message;
            return o;
        }

        static ColorMode? ParseColor(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "barva": case "barevne": case "barevně": case "color": return ColorMode.Color;
                case "seda": case "šedá": case "seda-skala": case "gray": case "grey": return ColorMode.Gray;
                case "cb": case "čb": case "bw": case "text": return ColorMode.BlackWhite;
                default: return null;
            }
        }

        public static string Describe(ColorMode m) =>
            m == ColorMode.Gray ? "odstíny šedi" : m == ColorMode.BlackWhite ? "černobíle" : "barevně";

        public const string ResultFileName = "result.txt";

        /// <summary>result.txt next to the output file, or null when the path is unusable.</summary>
        public static string ResultPathFor(string outputPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, ResultFileName);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                return null;
            }
        }

        /// <summary>Returns the full output path with an extension, or null when the extension is unsupported.</summary>
        public static string ResolveOutputPath(string path, out string formatId)
        {
            if (Path.GetExtension(path).Length == 0) path += ".jpg";
            formatId = ImageFormats.ForPath(path);
            return formatId == null ? null : Path.GetFullPath(path);
        }

        public static Choice ParseChoice(string input, int count, int defaultIndex)
        {
            string s = (input ?? "").Trim();
            if (s.Length == 0)
                return defaultIndex >= 0 && defaultIndex < count ? new Choice(ChoiceKind.Index, defaultIndex) : new Choice(ChoiceKind.Invalid);
            switch (s.ToLowerInvariant())
            {
                case "w": return new Choice(ChoiceKind.WindowsDialog);
                case "q": case "k": case "x": return new Choice(ChoiceKind.Quit);
            }
            if (int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= 1 && n <= count)
                return new Choice(ChoiceKind.Index, n - 1);
            return new Choice(ChoiceKind.Invalid);
        }

        /// <summary>Index of the configured scanner, or -1 when none is configured or it is not connected.</summary>
        public static int IndexOf(IList<string> deviceIds, string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < deviceIds.Count; i++)
                if (string.Equals(deviceIds[i], id, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public static string DescribeWiaError(int hresult)
        {
            switch (unchecked((uint)hresult))
            {
                case 0x80210001: return "obecná chyba skeneru";
                case 0x80210002: return "zaseknutý papír";
                case 0x80210003: return "v podavači není papír";
                case 0x80210004: return "problém s papírem";
                case 0x80210005: return "skener je vypnutý nebo odpojený";
                case 0x80210006: return "skener je zaneprázdněný";
                case 0x80210007: return "skener se zahřívá, zkuste to za chvíli";
                case 0x80210008: return "skener vyžaduje zásah uživatele";
                case 0x8021000A: return "chyba komunikace se skenerem";
                case 0x8021000C: return "nesprávné nastavení skeneru";
                case 0x8021000D: return "skener je zamčený jinou aplikací";
                case 0x80210015: return "není dostupný žádný skener";
                default: return "chyba 0x" + unchecked((uint)hresult).ToString("X8", CultureInfo.InvariantCulture);
            }
        }
    }
}
