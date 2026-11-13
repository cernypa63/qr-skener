using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SendFtp
{
    public interface ISifra
    {
        byte[] Zasifruj(byte[] data);
        byte[] Desifruj(byte[] data);
    }

    public sealed class ChybaNastaveni : Exception
    {
        public ChybaNastaveni(string zprava) : base(zprava) { }
    }

    /// <summary>Přístupové údaje k FTP, uložené zašifrovaně v SENDFTP.cfg vedle exe.</summary>
    public sealed class Nastaveni
    {
        public const string Soubor = "SENDFTP.cfg";
        private const string Hlavicka = "SENDFTP-CFG-1";

        public string Host = "";
        public int Port = 21;
        public string Login = "";
        public string Heslo = "";

        public void Uloz(string cesta, ISifra sifra)
        {
            var text = Host + "\n" + Port + "\n" + Login + "\n" + Heslo;
            var data = sifra.Zasifruj(Encoding.UTF8.GetBytes(text));
            File.WriteAllText(cesta, Hlavicka + "\r\n" + Convert.ToBase64String(data) + "\r\n", Encoding.ASCII);
        }

        public static Nastaveni Nacti(string cesta, ISifra sifra)
        {
            if (!File.Exists(cesta))
                throw new ChybaNastaveni("Přístupové údaje nejsou uložené. Nejdřív spusťte: SENDFTP.exe /nastav");
            try
            {
                var r = File.ReadAllLines(cesta);
                if (r.Length < 2 || r[0].Trim() != Hlavicka) throw new FormatException();
                var c = Encoding.UTF8.GetString(sifra.Desifruj(Convert.FromBase64String(r[1].Trim()))).Split(new[] { '\n' }, 4);
                if (c.Length != 4) throw new FormatException();
                return new Nastaveni { Host = c[0], Port = int.Parse(c[1]), Login = c[2], Heslo = c[3] };
            }
            catch (Exception e) when (e is FormatException || e is CryptographicException || e is IOException || e is UnauthorizedAccessException)
            {
                throw new ChybaNastaveni("Soubor " + Soubor + " nelze přečíst (je poškozený nebo vytvořený na jiném počítači). Uložte údaje znovu: SENDFTP.exe /nastav");
            }
        }

        /// <summary>Hodnoty host, port, login, heslo ze souboru parametrů klíč=hodnota.</summary>
        public static Nastaveni ZTextu(string text)
        {
            var h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var radek in text.Replace("\r\n", "\n").Split('\n'))
            {
                var r = radek.Trim();
                if (r.Length == 0 || r[0] == ';' || r[0] == '#') continue;
                var p = radek.IndexOf('=');
                if (p <= 0) throw new ChybaNastaveni("Řádek není ve tvaru klíč=hodnota: " + r);
                h[radek.Substring(0, p).Trim()] = radek.Substring(p + 1).Trim();
            }
            h.TryGetValue("port", out var port);
            return Over(h.TryGetValue("host", out var x) ? x : "", port, h.TryGetValue("login", out x) ? x : "",
                h.TryGetValue("heslo", out x) ? x : null);
        }

        public static Nastaveni Over(string host, string port, string login, string heslo)
        {
            host = (host ?? "").Trim();
            if (host.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)) host = host.Substring(6);
            host = host.TrimEnd('/');
            var n = new Nastaveni();
            var dv = host.LastIndexOf(':');
            if (dv > 0 && host.IndexOf(':') == dv)
            {
                port = host.Substring(dv + 1);
                host = host.Substring(0, dv);
            }
            if (host.Length == 0 || host.IndexOfAny(new[] { '/', ' ', '\\' }) >= 0) throw new ChybaNastaveni("Chybí nebo je neplatný host: " + host);
            if (!string.IsNullOrWhiteSpace(port) && (!int.TryParse(port.Trim(), out n.Port) || n.Port < 1 || n.Port > 65535))
                throw new ChybaNastaveni("Neplatný port: " + port);
            if (string.IsNullOrWhiteSpace(login)) throw new ChybaNastaveni("Chybí login.");
            if (heslo == null) throw new ChybaNastaveni("Chybí heslo.");
            n.Host = host;
            n.Login = login.Trim();
            n.Heslo = heslo;
            return n;
        }
    }
}
