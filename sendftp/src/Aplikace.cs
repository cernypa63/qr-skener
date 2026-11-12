using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FtpDir;

namespace SendFtp
{
    public interface IDotazy
    {
        string Cti(string vyzva, bool skryte);
        void Pis(string text);
    }

    /// <summary>
    /// SENDFTP.exe soubor adresar_na_ftp – odešle soubor (lze i masku, např. C:\A\*.pdf) na FTP.
    /// SENDFTP.exe /nastav [soubor_parametru] – uloží přístupové údaje zašifrovaně do SENDFTP.cfg.
    /// SENDFTP.exe /info – vypíše uložený server a login.
    /// Návratové kódy: 0 = OK, 2 = chybné parametry nebo chybí nastavení, 3 = chyba FTP, 4 = soubor k odeslání nenalezen.
    /// Při chybě je popis v SENDFTP.err vedle exe.
    /// </summary>
    public static class Aplikace
    {
        public const string SouborChyby = "SENDFTP.err";
        public const int TimeoutMs = 60000;

        private const string Pouziti =
            "Použití:\r\n" +
            "  SENDFTP.exe soubor adresar_na_ftp   odešle soubor (lze i maska, např. C:\\A\\*.pdf)\r\n" +
            "  SENDFTP.exe /nastav                 zadání a uložení přístupových údajů\r\n" +
            "  SENDFTP.exe /nastav parametry.txt   uložení údajů ze souboru (host=, port=, login=, heslo=)\r\n" +
            "  SENDFTP.exe /info                   vypíše uložený server a login";

        public static int Spust(string[] args, string adresarExe, ISifra sifra, IDotazy dotazy)
        {
            var err = Path.Combine(adresarExe, SouborChyby);
            var cfg = Path.Combine(adresarExe, Nastaveni.Soubor);
            try { File.Delete(err); } catch (Exception) { }

            int Chyba(int kod, string text)
            {
                dotazy.Pis(text);
                try { File.WriteAllText(err, DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + Environment.NewLine + text + Environment.NewLine, Encoding.GetEncoding(1250)); }
                catch (Exception) { }
                return kod;
            }

            try
            {
                if (args.Length >= 1 && args.Length <= 2 && Je(args[0], "/nastav"))
                {
                    var n = args.Length == 2 ? Nastaveni.ZTextu(CtiText(args[1])) : Zadej(dotazy);
                    using (var ftp = new FtpKlient(n.Host, n.Port, TimeoutMs)) ftp.Prihlas(n.Login, n.Heslo);
                    try { n.Uloz(cfg, sifra); }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        return Chyba(2, "Nastavení nelze uložit do " + cfg + ": " + e.Message);
                    }
                    dotazy.Pis("Přihlášení na " + n.Host + " je v pořádku, údaje jsou uložené v " + Nastaveni.Soubor + ".");
                    if (args.Length == 2) dotazy.Pis("Soubor " + args[1] + " s heslem teď smažte.");
                    return 0;
                }
                if (args.Length == 1 && Je(args[0], "/info"))
                {
                    var n = Nastaveni.Nacti(cfg, sifra);
                    dotazy.Pis("Server: " + n.Host + ":" + n.Port + ", login: " + n.Login);
                    return 0;
                }
                if (args.Length != 2 || Je(args[0], "/?"))
                    return Chyba(2, Pouziti);

                List<string> soubory;
                try { soubory = Soubory(args[0]); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
                {
                    return Chyba(4, "Soubor " + args[0] + " nelze najít: " + e.Message);
                }
                if (soubory.Count == 0) return Chyba(4, "Soubor k odeslání neexistuje: " + args[0]);

                var nast = Nastaveni.Nacti(cfg, sifra);
                using (var ftp = new FtpKlient(nast.Host, nast.Port, TimeoutMs))
                {
                    ftp.Prihlas(nast.Login, nast.Heslo);
                    ftp.ZmenAdresarVytvor(args[1]);
                    foreach (var s in soubory)
                    {
                        try { Odesli(ftp, s); }
                        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                        {
                            return Chyba(4, "Soubor " + s + " nelze přečíst: " + e.Message);
                        }
                        dotazy.Pis("Odesláno: " + s);
                    }
                }
                return 0;
            }
            catch (ChybaNastaveni e) { return Chyba(2, e.Message); }
            catch (ChybaFtp e) { return Chyba(3, e.Message); }
        }

        private static bool Je(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>Nahraje jako nazev.part a teprve celý přejmenuje, aby si na serveru nikdo nevzal neúplný soubor.</summary>
        private static void Odesli(FtpKlient ftp, string soubor)
        {
            var nazev = Path.GetFileName(soubor);
            var docasny = nazev + ".part";
            long delka;
            using (var f = File.OpenRead(soubor))
            {
                delka = f.Length;
                try { ftp.Uloz(docasny, f); }
                catch (ChybaFtp) { try { ftp.Smaz(docasny); } catch (Exception) { } throw; }
            }
            try
            {
                var naServeru = ftp.Velikost(docasny);
                if (naServeru.HasValue && naServeru.Value != delka)
                    throw new ChybaFtp("Soubor " + nazev + " se nepřenesl celý (" + naServeru + " z " + delka + " bajtů).");
                ftp.Smaz(nazev);
                ftp.Prejmenuj(docasny, nazev);
            }
            catch (ChybaFtp) { try { ftp.Smaz(docasny); } catch (Exception) { } throw; }
        }

        public static List<string> Soubory(string cesta)
        {
            var plna = Path.GetFullPath(cesta);
            var jmeno = Path.GetFileName(plna);
            if (jmeno.IndexOfAny(new[] { '*', '?' }) < 0)
                return File.Exists(plna) ? new List<string> { plna } : new List<string>();
            var adr = Path.GetDirectoryName(plna);
            if (!Directory.Exists(adr)) return new List<string>();
            var vysledek = new List<string>(Directory.GetFiles(adr, jmeno));
            vysledek.Sort(StringComparer.OrdinalIgnoreCase);
            return vysledek;
        }

        private static string CtiText(string soubor)
        {
            byte[] data;
            try { data = File.ReadAllBytes(soubor); }
            catch (Exception e) { throw new ChybaNastaveni("Soubor parametrů nelze načíst: " + e.Message); }
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Encoding.UTF8.GetString(data, 3, data.Length - 3);
            try { return new UTF8Encoding(false, true).GetString(data); }
            catch (DecoderFallbackException) { return Encoding.GetEncoding(1250).GetString(data); }
        }

        private static Nastaveni Zadej(IDotazy d) =>
            Nastaveni.Over(d.Cti("FTP server (host): ", false), d.Cti("Port [21]: ", false),
                d.Cti("Login: ", false), d.Cti("Heslo: ", true));
    }

    public sealed class KonzoleDotazy : IDotazy
    {
        public void Pis(string text) => Console.WriteLine(text);

        public string Cti(string vyzva, bool skryte)
        {
            Console.Write(vyzva);
            if (!skryte || Console.IsInputRedirected) return Console.ReadLine() ?? "";
            var sb = new StringBuilder();
            while (true)
            {
                var k = Console.ReadKey(true);
                if (k.Key == ConsoleKey.Enter) break;
                if (k.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0) { sb.Length--; Console.Write("\b \b"); }
                }
                else if (!char.IsControl(k.KeyChar)) { sb.Append(k.KeyChar); Console.Write('*'); }
            }
            Console.WriteLine();
            return sb.ToString();
        }
    }
}
