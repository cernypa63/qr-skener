using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FtpDir
{
    public sealed class Polozka
    {
        public string Nazev;
        public DateTime? Datum;
        public long Velikost;
        public bool Adresar;
    }

    /// <summary>Rozbor řádků příkazu LIST (formát Unix i Windows/IIS).</summary>
    public static class VypisFtp
    {
        private static readonly Regex Unix = new Regex(
            @"^(?<typ>[-dlcbps])\S{9}\S*\s+\d+\s+(?:\S+\s+){1,2}(?<vel>\d+)\s+(?<mes>[A-Za-z]{3})\s+(?<den>\d{1,2})\s+(?<cas>\d{1,2}:\d{2}|\d{4})\s(?<nazev>.+)$");

        private static readonly Regex Dos = new Regex(
            @"^(?<dat>\d{2}-\d{2}-\d{2,4})\s+(?<cas>\d{1,2}:\d{2}\s*(?:[AaPp][Mm])?)\s+(?<vel><DIR>|\d+)\s+(?<nazev>.+)$");

        private static readonly string[] Mesice =
            { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

        public static List<Polozka> Rozeber(IEnumerable<string> radky, DateTime ted)
        {
            var vysledek = new List<Polozka>();
            foreach (var r in radky)
            {
                var p = Radek(r.TrimEnd('\r', '\n'), ted);
                if (p != null && p.Nazev != "." && p.Nazev != "..") vysledek.Add(p);
            }
            return vysledek;
        }

        public static Polozka Radek(string r, DateTime ted)
        {
            var m = Unix.Match(r);
            if (m.Success)
            {
                var nazev = m.Groups["nazev"].Value;
                if (m.Groups["typ"].Value == "l")
                {
                    var s = nazev.IndexOf(" -> ", StringComparison.Ordinal);
                    if (s > 0) nazev = nazev.Substring(0, s);
                }
                int mes = Array.IndexOf(Mesice, m.Groups["mes"].Value.ToLowerInvariant()) + 1;
                int den = int.Parse(m.Groups["den"].Value, CultureInfo.InvariantCulture);
                var cas = m.Groups["cas"].Value;
                DateTime? datum = null;
                if (mes > 0)
                {
                    try
                    {
                        if (cas.IndexOf(':') < 0)
                            datum = new DateTime(int.Parse(cas, CultureInfo.InvariantCulture), mes, den);
                        else
                        {
                            var hm = cas.Split(':');
                            var d = new DateTime(ted.Year, mes, den, int.Parse(hm[0], CultureInfo.InvariantCulture),
                                int.Parse(hm[1], CultureInfo.InvariantCulture), 0);
                            datum = d > ted.AddDays(2) ? d.AddYears(-1) : d;
                        }
                    }
                    catch (ArgumentOutOfRangeException) { }
                }
                return new Polozka
                {
                    Nazev = nazev,
                    Adresar = m.Groups["typ"].Value == "d",
                    Velikost = long.Parse(m.Groups["vel"].Value, CultureInfo.InvariantCulture),
                    Datum = datum,
                };
            }

            m = Dos.Match(r);
            if (m.Success)
            {
                var text = m.Groups["dat"].Value + " " + Regex.Replace(m.Groups["cas"].Value, @"\s+", "").ToUpperInvariant();
                DateTime d;
                var ok = DateTime.TryParseExact(text,
                    new[] { "MM-dd-yy h:mmtt", "MM-dd-yyyy h:mmtt", "MM-dd-yy H:mm", "MM-dd-yyyy H:mm" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
                var dir = m.Groups["vel"].Value == "<DIR>";
                return new Polozka
                {
                    Nazev = m.Groups["nazev"].Value,
                    Adresar = dir,
                    Velikost = dir ? 0 : long.Parse(m.Groups["vel"].Value, CultureInfo.InvariantCulture),
                    Datum = ok ? d : (DateTime?)null,
                };
            }
            return null;
        }

        /// <summary>Odpověď na MDTM: "213 YYYYMMDDHHMMSS[.sss]".</summary>
        public static DateTime? Mdtm(string odpoved)
        {
            var m = Regex.Match(odpoved ?? "", @"^213\s+(\d{14})");
            return m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null;
        }
    }
}
