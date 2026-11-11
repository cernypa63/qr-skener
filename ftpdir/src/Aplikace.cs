using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FtpDir
{
    /// <summary>
    /// FTPDIR.exe soubor_parametru – vypíše soubory adresáře na FTP do CSV (soubor;datum;velikost).
    /// Návratové kódy: 0 = OK, 2 = chybný soubor parametrů, 3 = chyba FTP, 4 = výstup nelze zapsat.
    /// Při chybě se výstupní soubor smaže a popis chyby je v FTPDIR.err vedle exe.
    /// </summary>
    public static class Aplikace
    {
        public const string SouborChyby = "FTPDIR.err";
        public const string FormatData = "dd.MM.yyyy HH:mm:ss";

        public static int Spust(string[] args, string adresarExe)
        {
            var err = Path.Combine(adresarExe, SouborChyby);
            try { File.Delete(err); } catch (Exception) { }

            if (args.Length != 1)
            {
                ZapisChybu(err, "Program se volá s jedním parametrem: FTPDIR.exe cesta_k_souboru_parametru");
                return 2;
            }

            Vstup v;
            try { v = Vstup.Nacti(args[0]); }
            catch (ChybaVstupu e)
            {
                ZapisChybu(err, e.Message + Environment.NewLine + "Soubor: " + args[0]);
                return 2;
            }

            try { File.Delete(v.Output); }
            catch (Exception e) when (!(e is DirectoryNotFoundException))
            {
                ZapisChybu(err, "Výstupní soubor " + v.Output + " nelze přepsat: " + e.Message);
                return 4;
            }
            catch (DirectoryNotFoundException) { }

            List<Polozka> soubory;
            try { soubory = NactiSeznam(v); }
            catch (ChybaFtp e)
            {
                ZapisChybu(err, e.Message + Environment.NewLine + "Server: " + v.Host + ":" + v.Port + ", adresář: " + v.Adresar);
                return 3;
            }

            var tmp = v.Output + ".tmp";
            try
            {
                File.WriteAllText(tmp, Csv(soubory, v.Hlavicka), Encoding.GetEncoding(1250));
                File.Move(tmp, v.Output);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                try { File.Delete(tmp); } catch (Exception) { }
                ZapisChybu(err, "Výstupní soubor " + v.Output + " nelze zapsat: " + e.Message);
                return 4;
            }
            return 0;
        }

        public static List<Polozka> NactiSeznam(Vstup v)
        {
            using (var ftp = new FtpKlient(v.Host, v.Port, v.TimeoutSekund * 1000))
            {
                ftp.Prihlas(v.Login, v.Heslo);
                ftp.ZmenAdresar(v.Adresar);
                var maska = Vstup.MaskaNaRegex(v.Maska);
                var soubory = VypisFtp.Rozeber(ftp.List(), DateTime.Now)
                    .FindAll(p => !p.Adresar && maska.IsMatch(p.Nazev));
                var presne = ftp.Mdtm(soubory.ConvertAll(p => p.Nazev));
                foreach (var p in soubory)
                    if (presne.TryGetValue(p.Nazev, out var d)) p.Datum = d;
                soubory.Sort((a, b) => string.CompareOrdinal(a.Nazev, b.Nazev));
                return soubory;
            }
        }

        public static string Csv(List<Polozka> soubory, bool hlavicka)
        {
            var sb = new StringBuilder();
            if (hlavicka) sb.Append("soubor;datum;velikost\r\n");
            foreach (var p in soubory)
                sb.Append(Pole(p.Nazev)).Append(';')
                  .Append(p.Datum?.ToString(FormatData, System.Globalization.CultureInfo.InvariantCulture) ?? "").Append(';')
                  .Append(p.Velikost).Append("\r\n");
            return sb.ToString();
        }

        private static string Pole(string s) =>
            s.IndexOfAny(new[] { ';', '"', '\r', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

        private static void ZapisChybu(string soubor, string text)
        {
            try
            {
                File.WriteAllText(soubor, DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + Environment.NewLine + text + Environment.NewLine,
                    Encoding.GetEncoding(1250));
            }
            catch (Exception) { }
        }
    }
}
