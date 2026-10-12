using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace U2dos
{
    static class ExitCodes
    {
        public const int Ok = 0;
        public const int Usage = 1;
        public const int InputMissing = 2;
        public const int IoError = 3;
    }

    /// <summary>Converts UTF-8 text with LF line ends to Latin II (cp852) with CRLF.</summary>
    static class Converter
    {
        public const string Usage = "Použití: u2dos <vstup UTF-8/LF> <výstup cp852/CRLF>";

        /// <summary>Typographic characters missing in cp852, replaced by ASCII look-alikes; anything else unmappable becomes '?'.</summary>
        static readonly Dictionary<char, string> Substitutes = new Dictionary<char, string>
        {
            ['\u201E'] = "\"", ['\u201C'] = "\"", ['\u201D'] = "\"", ['\u00AB'] = "\"", ['\u00BB'] = "\"",
            ['\u201A'] = "'", ['\u2018'] = "'", ['\u2019'] = "'", ['\u2039'] = "'", ['\u203A'] = "'",
            ['\u2013'] = "-", ['\u2014'] = "-", ['\u2212'] = "-", ['\u2010'] = "-", ['\u2011'] = "-",
            ['\u2026'] = "...", ['\u20AC'] = "EUR", ['\u2022'] = "*", ['\u2122'] = "TM",
            ['\u2009'] = " ", ['\u202F'] = " ", ['\u2002'] = " ", ['\u2003'] = " ",
        };

        public static Encoding Cp852() =>
            Encoding.GetEncoding(852, new EncoderReplacementFallback("?"), DecoderFallback.ReplacementFallback);

        public static int Run(string[] args, TextWriter errors)
        {
            if (args.Length != 2)
            {
                errors.WriteLine(Usage);
                return ExitCodes.Usage;
            }
            try
            {
                string input = Path.GetFullPath(args[0]);
                string output = Path.GetFullPath(args[1]);
                if (!File.Exists(input))
                {
                    errors.WriteLine("Chyba: vstupní soubor " + input + " neexistuje.");
                    return ExitCodes.InputMissing;
                }
                if (string.Equals(input, output, StringComparison.OrdinalIgnoreCase))
                {
                    errors.WriteLine("Chyba: vstupní a výstupní soubor musí být různé.");
                    return ExitCodes.Usage;
                }
                string dir = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                using (var reader = new StreamReader(input, new UTF8Encoding(false, false), true))
                using (var writer = new StreamWriter(output, false, Cp852()))
                    Convert(reader, writer);
                return ExitCodes.Ok;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                errors.WriteLine("Chyba: " + e.Message);
                return ExitCodes.IoError;
            }
        }

        /// <summary>LF → CRLF (existing CRLF is kept), unmappable characters substituted.</summary>
        public static void Convert(TextReader reader, TextWriter writer)
        {
            var buffer = new char[65536];
            bool previousCr = false;
            int n;
            while ((n = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    char c = buffer[i];
                    if (c == '\n' && !previousCr) writer.Write('\r');
                    if (Substitutes.TryGetValue(c, out string s)) writer.Write(s);
                    else if (char.IsHighSurrogate(c) || char.IsLowSurrogate(c))
                    {
                        if (char.IsHighSurrogate(c)) writer.Write('?');
                    }
                    else writer.Write(c);
                    previousCr = c == '\r';
                }
            }
        }
    }
}
