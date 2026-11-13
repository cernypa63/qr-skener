using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FtpDir
{
    /// <summary>
    /// FTPDIR.exe soubor_parametru – vypíše soubory adresáře na FTP do CSV (soubor;datum;velikost).
    /// Návratové kódy: 0 = OK, 2 = chybný soubor parametrů, 3 = chyba FTP, 4 = výstup nelze zapsat,
    /// 5 = CSV zapsáno, ale server nevypsal celý adresář (seznam může být neúplný).
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
            bool neuplny;
            try { soubory = NactiSeznam(v, out neuplny); }
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
            if (neuplny)
            {
                ZapisChybu(err, "Server vypisuje jen omezený počet souborů a celý adresář se nepodařilo načíst. Seznam v "
                    + v.Output + " může být neúplný (" + soubory.Count + " souborů)." + Environment.NewLine
                    + "Server: " + v.Host + ":" + v.Port + ", adresář: " + v.Adresar);
                return 5;
            }
            return 0;
        }

        public static List<Polozka> NactiSeznam(Vstup v) => NactiSeznam(v, out _);

        public static List<Polozka> NactiSeznam(Vstup v, out bool neuplny)
        {
            using (var ftp = new FtpKlient(v.Host, v.Port, v.TimeoutSekund * 1000))
            {
                ftp.Prihlas(v.Login, v.Heslo);
                ftp.ZmenAdresar(v.Adresar);
                var maska = Vstup.MaskaNaRegex(v.Maska);
                var soubory = CelyVypis(ftp, out neuplny)
                    .FindAll(p => !p.Adresar && maska.IsMatch(p.Nazev));
                var presne = ftp.Mdtm(soubory.ConvertAll(p => p.Nazev));
                foreach (var p in soubory)
                    if (presne.TryGetValue(p.Nazev, out var d)) p.Datum = d;
                soubory.Sort((a, b) => string.CompareOrdinal(a.Nazev, b.Nazev));
                return soubory;
            }
        }

        /// <summary>
        /// Když server výpis zkrátí (cesky-hosting: max. 3500 řádků), vypíše se adresář ještě obráceně podle názvu (LIST -r).
        /// Pokud se oba výpisy překrývají, je seznam úplný; jinak se zkusí i řazení podle času a velikosti.
        /// </summary>
        private static List<Polozka> CelyVypis(FtpKlient ftp, out bool neuplny)
        {
            var ted = DateTime.Now;
            var vzestupne = VypisFtp.Rozeber(ftp.List(), ted);
            neuplny = false;
            if (!ftp.PosledniVypisZkraceny) return vzestupne;

            var vse = new Dictionary<string, Polozka>(StringComparer.Ordinal);
            foreach (var p in vzestupne) vse[p.Nazev] = p;
            var prvni = new HashSet<string>(vse.Keys, StringComparer.Ordinal);

            neuplny = true;
            foreach (var volby in new[] { "-r", "-t", "-tr", "-S", "-Sr" })
            {
                List<Polozka> cast;
                bool zkraceny;
                try
                {
                    cast = VypisFtp.Rozeber(ftp.List(volby), ted);
                    zkraceny = ftp.PosledniVypisZkraceny;
                }
                catch (ChybaFtp) { break; }
                var prekryv = false;
                foreach (var p in cast)
                {
                    if (prvni.Contains(p.Nazev)) prekryv = true;
                    vse[p.Nazev] = p;
                }
                if (!zkraceny || (volby == "-r" && prekryv)) { neuplny = false; break; }
            }
            return new List<Polozka>(vse.Values);
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
