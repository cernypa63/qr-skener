using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Rozeslat
{
    public sealed class Tabulka
    {
        public List<string[]> Radky = new List<string[]>();
        public char Oddelovac;
        public Encoding Kodovani;
        public int PocetSloupcu => Radky.Count == 0 ? 0 : Radky.Max(r => r.Length);
    }

    public sealed class VysledekAdres
    {
        public List<string> Platne = new List<string>();
        public List<string> Neplatne = new List<string>();
        public int Duplicity;
    }

    public static class SeznamAdres
    {
        static readonly Regex EmailRe = new Regex(
            @"(?<![A-Za-z0-9._%+\-!#$&*/=?^`{|}~])[A-Za-z0-9_%+\-][A-Za-z0-9._%+\-']*@[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?)*\.[A-Za-z]{2,}",
            RegexOptions.CultureInvariant);

        public static Tabulka NactiSoubor(string cesta) => NactiBajty(File.ReadAllBytes(cesta));

        public static Tabulka NactiBajty(byte[] data)
        {
            var t = new Tabulka();
            string text;
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                t.Kodovani = new UTF8Encoding(false);
                text = t.Kodovani.GetString(data, 3, data.Length - 3);
            }
            else
            {
                try
                {
                    text = new UTF8Encoding(false, true).GetString(data);
                    t.Kodovani = new UTF8Encoding(false);
                }
                catch (DecoderFallbackException)
                {
                    t.Kodovani = Encoding.GetEncoding(1250);
                    text = t.Kodovani.GetString(data);
                }
            }
            t.Oddelovac = UrciOddelovac(text);
            t.Radky = Rozdel(text, t.Oddelovac);
            return t;
        }

        // Oddělovač, kterého je v prvních 20 záznamech (mimo uvozovky) nejvíc.
        static char UrciOddelovac(string text)
        {
            var kandidati = new[] { ';', '\t', ',' };
            var pocty = new int[kandidati.Length];
            bool uv = false;
            int zaznamu = 0;
            for (int i = 0; i < text.Length && zaznamu < 20; i++)
            {
                char z = text[i];
                if (z == '"') uv = !uv;
                else if (!uv && z == '\n') zaznamu++;
                else if (!uv)
                    for (int k = 0; k < kandidati.Length; k++)
                        if (z == kandidati[k]) pocty[k]++;
            }
            int nej = 0;
            for (int k = 1; k < kandidati.Length; k++)
                if (pocty[k] > pocty[nej]) nej = k;
            return kandidati[nej];
        }

        static List<string[]> Rozdel(string text, char odd)
        {
            var radky = new List<string[]>();
            var pole = new List<string>();
            var sb = new StringBuilder();
            bool uv = false, neco = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (uv)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else uv = false;
                    }
                    else sb.Append(c);
                    continue;
                }
                if (c == '"') { uv = true; neco = true; }
                else if (c == odd) { pole.Add(sb.ToString()); sb.Clear(); neco = true; }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    pole.Add(sb.ToString()); sb.Clear();
                    if (neco || pole.Count > 1 || pole[0].Trim().Length > 0) radky.Add(pole.ToArray());
                    pole.Clear(); neco = false;
                }
                else { sb.Append(c); neco = true; }
            }
            if (neco || sb.Length > 0)
            {
                pole.Add(sb.ToString());
                if (pole.Count > 1 || pole[0].Trim().Length > 0) radky.Add(pole.ToArray());
            }
            return radky;
        }

        static string Bunka(string[] r, int s) => s < r.Length ? r[s].Trim() : "";

        public static bool MaZahlavi(Tabulka t) =>
            t.Radky.Count > 1 && !t.Radky[0].Any(b => b.Contains("@"));

        public static List<string> NazvySloupcu(Tabulka t, bool zahlavi)
        {
            var n = new List<string>();
            for (int s = 0; s < t.PocetSloupcu; s++)
            {
                string h = zahlavi && t.Radky.Count > 0 ? Bunka(t.Radky[0], s) : "";
                string uk = t.Radky.Skip(zahlavi ? 1 : 0).Select(r => Bunka(r, s)).FirstOrDefault(b => b.Length > 0) ?? "";
                if (uk.Length > 30) uk = uk.Substring(0, 30) + "...";
                string nazev = "Sloupec " + (s + 1);
                if (h.Length > 0) nazev += ": " + h;
                if (uk.Length > 0) nazev += "  (např. " + uk + ")";
                n.Add(nazev);
            }
            return n;
        }

        public static int NajdiSloupecSEmaily(Tabulka t)
        {
            int nej = 0, max = -1;
            for (int s = 0; s < t.PocetSloupcu; s++)
            {
                int n = t.Radky.Count(r => EmailRe.IsMatch(Bunka(r, s)));
                if (n > max) { max = n; nej = s; }
            }
            return nej;
        }

        public static VysledekAdres Adresy(Tabulka t, int sloupec, bool zahlavi)
        {
            var v = new VysledekAdres();
            var videno = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = zahlavi ? 1 : 0; i < t.Radky.Count; i++)
            {
                string b = Bunka(t.Radky[i], sloupec);
                if (b.Length == 0) continue;
                var shody = EmailRe.Matches(b);
                if (shody.Count == 0) { v.Neplatne.Add("řádek " + (i + 1) + ": " + b); continue; }
                foreach (Match m in shody)
                {
                    string a = m.Value;
                    if (videno.Add(a)) v.Platne.Add(a); else v.Duplicity++;
                }
            }
            return v;
        }

        public static bool JePlatnaAdresa(string a)
        {
            var m = EmailRe.Match(a ?? "");
            return m.Success && m.Value == a.Trim();
        }
    }
}
