using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Fwait
{
    static class ExitCodes
    {
        public const int Ok = 0;
        public const int Usage = 1;
        public const int Timeout = 2;
        public const int CopyFailed = 3;
    }

    /// <summary>fwait &lt;soubor&gt; &lt;sekundy&gt; &lt;cíl&gt; – silently waits for a file and copies it; errors go into the target file.</summary>
    static class FileWaiter
    {
        const int PollMilliseconds = 200;

        public static int Run(string[] args)
        {
            if (args.Length != 3)
            {
                if (args.Length > 3) WriteError(args[2], "Chyba: očekávány 3 parametry: <soubor> <sekundy> <cíl>.");
                return ExitCodes.Usage;
            }

            string source = args[0], target = args[2];
            if (!TryParseSeconds(args[1], out double seconds))
            {
                WriteError(target, "Chyba: neplatný počet sekund '" + args[1] + "'.");
                return ExitCodes.Usage;
            }

            try
            {
                source = Path.GetFullPath(source);
                target = ResolveTarget(source, target);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                WriteError(target, "Chyba: neplatná cesta: " + e.Message);
                return ExitCodes.Usage;
            }

            var clock = Stopwatch.StartNew();
            var limit = TimeSpan.FromSeconds(seconds);
            while (true)
            {
                if (File.Exists(source) && IsReady(source)) return Copy(source, target);
                if (clock.Elapsed >= limit) break;
                Thread.Sleep(PollMilliseconds);
            }

            WriteError(target, File.Exists(source)
                ? "Chyba: soubor " + source + " je stále používán jiným programem (čekáno " + FormatSeconds(seconds) + " s)."
                : "Chyba: soubor " + source + " se do " + FormatSeconds(seconds) + " s neobjevil.");
            return ExitCodes.Timeout;
        }

        public static bool TryParseSeconds(string s, out double seconds)
        {
            bool ok = double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out seconds);
            return ok && seconds >= 0 && seconds <= 24 * 3600;
        }

        /// <summary>An existing directory as target means "copy into it under the same name".</summary>
        static string ResolveTarget(string source, string target)
        {
            string full = Path.GetFullPath(target);
            return Directory.Exists(full) ? Path.Combine(full, Path.GetFileName(source)) : full;
        }

        /// <summary>False while another program still has the file open for writing.</summary>
        static bool IsReady(string path)
        {
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        static int Copy(string source, string target)
        {
            try
            {
                if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return ExitCodes.Ok;
                string dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.Copy(source, target, true);
                return ExitCodes.Ok;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                WriteError(target, "Chyba: soubor " + source + " nelze zkopírovat do " + target + ": " + e.Message);
                return ExitCodes.CopyFailed;
            }
        }

        static void WriteError(string target, string message)
        {
            try
            {
                string full = Path.GetFullPath(target);
                string dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(full, message + "\r\n", new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        static string FormatSeconds(double s) => s.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
