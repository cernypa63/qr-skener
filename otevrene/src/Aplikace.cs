using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Otevrene
{
    public class OtevrenySoubor
    {
        public ulong Id;
        public string Ip = "";
        public string Uzivatel = "";
        public string Cesta = "";
    }

    public class Sezeni
    {
        public string Ip = "";
        public string Uzivatel = "";
    }

    public class PristupOdepren : Exception
    {
        public PristupOdepren(string text) : base(text) { }
    }

    /// <summary>
    /// OTEVRENE.exe [text|maska] [/net] - vypíše soubory otevřené přes síť (IP adresa, uživatel, ID, soubor).
    /// Návratové kódy: 0 = něco je otevřené, 1 = nic otevřené, 2 = chybné parametry, 3 = chybí práva správce, 5 = jiná chyba.
    /// </summary>
    public static class Aplikace
    {
        public const string Napoveda =
            "Pouziti: OTEVRENE.exe [text|maska] [/net]\r\n" +
            "Vypise soubory, ktere maji pres sit otevrene jine pocitace: IP adresa, uzivatel, ID, soubor.\r\n" +
            "  text   vypise jen radky, kde je text v souboru, IP adrese nebo uzivateli (napr. D:\\DATA nebo 192.168.1.15)\r\n" +
            "  maska  s * a ?, napr. D:\\DATA\\*.DBF\r\n" +
            "  /net   starsi zpusob zjisteni (jako na Windows Serveru 2008)\r\n" +
            "Spoustejte jako spravce. Soubor jde odpojit prikazem: net file ID /close";

        /// <param name="nacti">jenNetApi -> seznam otevřených souborů a popis použitého způsobu</param>
        public static int Spust(string[] args, Func<bool, KeyValuePair<string, List<OtevrenySoubor>>> nacti, TextWriter vystup)
        {
            var jenNetApi = false;
            string filtr = null;
            foreach (var a in args)
            {
                if (a == "/?" || a == "-?" || a.Equals("/h", StringComparison.OrdinalIgnoreCase))
                {
                    vystup.WriteLine(Napoveda);
                    return 2;
                }
                if (a.Equals("/net", StringComparison.OrdinalIgnoreCase)) jenNetApi = true;
                else if (a.Trim().Length == 0) continue;
                else if (filtr == null) filtr = a.Trim();
                else
                {
                    vystup.WriteLine("Prilis mnoho parametru: " + a);
                    vystup.WriteLine(Napoveda);
                    return 2;
                }
            }

            KeyValuePair<string, List<OtevrenySoubor>> data;
            try { data = nacti(jenNetApi); }
            catch (PristupOdepren e)
            {
                vystup.WriteLine(e.Message);
                vystup.WriteLine("Spustte prikazovy radek jako spravce (pravym tlacitkem - Spustit jako spravce).");
                return 3;
            }
            catch (Exception e)
            {
                vystup.WriteLine("Seznam otevrenych souboru nelze zjistit: " + e.Message);
                return 5;
            }

            var seznam = data.Value.Where(s => filtr == null || Odpovida(s, filtr))
                .OrderBy(s => IpKlic(s.Ip), StringComparer.Ordinal)
                .ThenBy(s => s.Cesta, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (seznam.Count == 0)
            {
                vystup.WriteLine(filtr == null ? "Zadny soubor neni otevreny pres sit." : "Zadny otevreny soubor neodpovida: " + filtr);
                return 1;
            }
            vystup.Write(Tabulka(seznam));
            vystup.WriteLine();
            vystup.WriteLine("Otevrenych souboru: " + seznam.Count + "  (" + data.Key + ")");
            return 0;
        }

        public static string Tabulka(IList<OtevrenySoubor> seznam)
        {
            var hlavicka = new[] { "IP adresa", "Uzivatel", "ID", "Soubor" };
            var radky = seznam.Select(s => new[] { s.Ip, s.Uzivatel, s.Id.ToString(), s.Cesta }).ToList();
            var sirky = new int[3];
            for (var i = 0; i < 3; i++)
                sirky[i] = Math.Max(hlavicka[i].Length, radky.Max(r => r[i].Length));
            var sb = new StringBuilder();
            foreach (var r in new[] { hlavicka }.Concat(radky))
                sb.Append(r[0].PadRight(sirky[0])).Append("  ").Append(r[1].PadRight(sirky[1])).Append("  ")
                  .Append(r[2].PadLeft(sirky[2])).Append("  ").Append(r[3]).Append("\r\n");
            return sb.ToString();
        }

        public static bool Odpovida(OtevrenySoubor s, string filtr)
        {
            var pole = new[] { s.Cesta, s.Ip, s.Uzivatel };
            if (filtr.IndexOfAny(new[] { '*', '?' }) < 0)
                return pole.Any(p => p.IndexOf(filtr, StringComparison.OrdinalIgnoreCase) >= 0);
            var re = new Regex("^" + Regex.Escape(filtr).Replace("\\*", ".*").Replace("\\?", ".") + "$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return pole.Any(p => re.IsMatch(p));
        }

        /// <summary>Starší Windows u souboru IP neuvádí; přiřadí se podle uživatele z otevřených sezení.</summary>
        public static void DoplnIp(IList<OtevrenySoubor> soubory, IList<Sezeni> sezeni)
        {
            foreach (var s in soubory)
            {
                var ip = sezeni.Where(z => z.Uzivatel.Equals(s.Uzivatel, StringComparison.OrdinalIgnoreCase))
                    .Select(z => z.Ip).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => IpKlic(x), StringComparer.Ordinal).ToArray();
                s.Ip = ip.Length == 0 ? "?" : ip.Length == 1 ? ip[0] : string.Join(" nebo ", ip);
            }
        }

        public static string BezLomitek(string jmeno) => (jmeno ?? "").TrimStart('\\');

        /// <summary>Řazení IPv4 podle čísel (192.168.1.9 před 192.168.1.10).</summary>
        private static string IpKlic(string ip)
        {
            var c = ip.Split('.');
            if (c.Length != 4) return "~" + ip;
            var sb = new StringBuilder();
            foreach (var x in c)
            {
                int n;
                if (!int.TryParse(x, out n)) return "~" + ip;
                sb.Append(n.ToString("000"));
            }
            return sb.ToString();
        }
    }
}
