using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FtpDir
{
    public sealed class ChybaVstupu : Exception
    {
        public ChybaVstupu(string zprava) : base(zprava) { }
    }

    /// <summary>
    /// Soubor parametrů: řádky klíč=hodnota (Windows-1250 nebo UTF-8), prázdné řádky
    /// a řádky začínající ; nebo # se ignorují.
    /// </summary>
    public sealed class Vstup
    {
        public string Host = "";
        public int Port = 21;
        public string Login = "";
        public string Heslo = "";
        public string Adresar = "";
        public string Output = "";
        public string Maska = "*";
        public bool Hlavicka = true;
        public int TimeoutSekund = 30;

        private static readonly string[] Zname =
            { "host", "port", "login", "heslo", "adresar", "output", "maska", "hlavicka", "timeout" };

        public static Vstup Nacti(string soubor)
        {
            byte[] data;
            try { data = File.ReadAllBytes(soubor); }
            catch (Exception e) { throw new ChybaVstupu("Soubor parametrů nelze načíst: " + e.Message); }
            return Rozeber(Dekoduj(data));
        }

        public static string Dekoduj(byte[] data)
        {
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Encoding.UTF8.GetString(data, 3, data.Length - 3);
            try { return new UTF8Encoding(false, true).GetString(data); }
            catch (DecoderFallbackException) { return Encoding.GetEncoding(1250).GetString(data); }
        }

        public static Vstup Rozeber(string text)
        {
            var h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var radky = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < radky.Length; i++)
            {
                var r = radky[i].Trim();
                if (r.Length == 0 || r[0] == ';' || r[0] == '#') continue;
                var p = r.IndexOf('=');
                if (p <= 0) throw new ChybaVstupu("Řádek " + (i + 1) + " není ve tvaru klíč=hodnota: " + r);
                var klic = r.Substring(0, p).Trim();
                if (Array.IndexOf(Zname, klic.ToLowerInvariant()) < 0)
                    throw new ChybaVstupu("Neznámý parametr na řádku " + (i + 1) + ": " + klic
                        + " (povolené: " + string.Join(", ", Zname) + ").");
                if (h.ContainsKey(klic)) throw new ChybaVstupu("Parametr " + klic + " je uveden vícekrát.");
                h[klic] = radky[i].Substring(radky[i].IndexOf('=') + 1).Trim();
            }

            string Povinny(string k) =>
                h.TryGetValue(k, out var x) && x.Length > 0 ? x : throw new ChybaVstupu("Chybí povinný parametr " + k + "=");

            var v = new Vstup
            {
                Login = Povinny("login"),
                Heslo = h.TryGetValue("heslo", out var hs) ? hs : throw new ChybaVstupu("Chybí povinný parametr heslo="),
                Adresar = Povinny("adresar"),
                Output = Povinny("output"),
            };

            var host = Regex.Replace(Povinny("host"), "^ftp://", "", RegexOptions.IgnoreCase).TrimEnd('/');
            var dv = host.LastIndexOf(':');
            if (dv > 0 && host.IndexOf(':') == dv)
            {
                v.Port = Cislo(host.Substring(dv + 1), "port v host", 1, 65535);
                host = host.Substring(0, dv);
            }
            if (host.Length == 0 || host.IndexOfAny(new[] { '/', ' ', '\\' }) >= 0)
                throw new ChybaVstupu("Neplatný host: " + h["host"]);
            v.Host = host;

            if (h.TryGetValue("port", out var port) && port.Length > 0) v.Port = Cislo(port, "port", 1, 65535);
            if (h.TryGetValue("timeout", out var t) && t.Length > 0) v.TimeoutSekund = Cislo(t, "timeout", 1, 600);
            if (h.TryGetValue("maska", out var m) && m.Length > 0) v.Maska = m;
            if (h.TryGetValue("hlavicka", out var hl) && hl.Length > 0)
            {
                if (hl != "0" && hl != "1") throw new ChybaVstupu("Parametr hlavicka musí být 0 nebo 1: " + hl);
                v.Hlavicka = hl == "1";
            }

            try { v.Output = Path.GetFullPath(v.Output); }
            catch (Exception e) { throw new ChybaVstupu("Neplatná cesta output: " + v.Output + " (" + e.Message + ")"); }
            return v;
        }

        private static int Cislo(string s, string nazev, int min, int max) =>
            int.TryParse(s, out var n) && n >= min && n <= max
                ? n
                : throw new ChybaVstupu("Parametr " + nazev + " musí být číslo " + min + " až " + max + ": " + s);

        /// <summary>Maska s * a ?, více masek oddělených ; nebo , (např. *.pdf;*.xml). Velikost písmen se nerozlišuje.</summary>
        public static Regex MaskaNaRegex(string maska)
        {
            var casti = new List<string>();
            foreach (var c in maska.Split(';', ','))
            {
                var x = c.Trim();
                if (x.Length > 0) casti.Add(Regex.Escape(x).Replace(@"\*", ".*").Replace(@"\?", "."));
            }
            if (casti.Count == 0) casti.Add(".*");
            return new Regex("^(?:" + string.Join("|", casti) + ")$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
