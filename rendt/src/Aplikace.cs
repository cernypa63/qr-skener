using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace RenDt
{
    /// <summary>
    /// RENDT.exe soubor – přejmenuje C:\A\faktura.pdf na C:\A\faktura_YYMMDD_hhmmss.pdf (aktuální datum a čas).
    /// Nové jméno vypíše na výstup. Návratové kódy: 0 = OK, 2 = chybné parametry,
    /// 3 = soubor s novým jménem už existuje, 4 = soubor neexistuje nebo nejde přejmenovat. Chyba je i v RENDT.err.
    /// </summary>
    public static class Aplikace
    {
        public const string SouborChyby = "RENDT.err";

        public static int Spust(string[] args, string adresarExe, Func<DateTime> ted, TextWriter vystup)
        {
            var err = Path.Combine(adresarExe, SouborChyby);
            try { File.Delete(err); } catch (Exception) { }

            int Chyba(int kod, string text)
            {
                vystup.WriteLine(text);
                try { File.WriteAllText(err, text + Environment.NewLine, Encoding.GetEncoding(1250)); }
                catch (Exception) { }
                return kod;
            }

            if (args.Length != 1 || args[0].Trim().Length == 0 || args[0] == "/?")
                return Chyba(2, "Použití: RENDT.exe cesta_k_souboru\r\nPřejmenuje soubor na jmeno_YYMMDD_hhmmss.přípona.");

            string zdroj, cil;
            try
            {
                zdroj = Path.GetFullPath(args[0].Trim());
                if (!File.Exists(zdroj))
                    return Chyba(4, Directory.Exists(zdroj) ? "Zadaná cesta je adresář, ne soubor: " + zdroj : "Soubor neexistuje: " + zdroj);
                cil = Path.Combine(Path.GetDirectoryName(zdroj), NoveJmeno(Path.GetFileName(zdroj), ted()));
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is IOException || e is UnauthorizedAccessException)
            {
                return Chyba(2, "Neplatná cesta k souboru: " + args[0] + " (" + e.Message + ")");
            }

            if (File.Exists(cil) || Directory.Exists(cil))
                return Chyba(3, "Soubor s novým jménem už existuje: " + cil);
            try { File.Move(zdroj, cil); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return Chyba(4, "Soubor " + zdroj + " nelze přejmenovat: " + e.Message);
            }
            vystup.WriteLine(cil);
            return 0;
        }

        /// <summary>faktura.pdf -> faktura_261112_094300.pdf; formát pevný, nezávislý na nastavení Windows.</summary>
        public static string NoveJmeno(string jmeno, DateTime cas)
        {
            var razitko = "_" + cas.ToString("yyMMdd'_'HHmmss", CultureInfo.InvariantCulture);
            var tecka = jmeno.LastIndexOf('.');
            return tecka <= 0 ? jmeno + razitko : jmeno.Substring(0, tecka) + razitko + jmeno.Substring(tecka);
        }
    }
}
