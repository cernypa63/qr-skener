using System;
using System.Globalization;
using System.IO;

namespace TzCz
{
    /// <summary>
    /// TZCZ.exe [-iso] [datum [čas]] – vypíše posun českého času proti UTC: +01:00 (zimní) nebo +02:00 (letní).
    /// Bez data platí pro aktuální okamžik. Počítá podle pravidla EU (od roku 1996), ne podle nastavení Windows.
    /// Návratové kódy: 1 = zimní čas, 2 = letní čas, 3 = chybné parametry. Přepínač -- = nic nevypisovat.
    /// </summary>
    public static class Aplikace
    {
        public const int Zimni = 1, Letni = 2, ChybaParametru = 3;
        public const int PrvniRok = 1996;

        private static readonly string[] FormatyData = { "yyyy-MM-dd", "d.M.yyyy" };
        private static readonly string[] FormatyCasu = { "H:mm", "H:mm:ss" };

        public static int Spust(string[] args, Func<DateTime> utcTed, TextWriter vystup)
        {
            if (Array.IndexOf(args, "--") >= 0)
            {
                var zbytek = Array.FindAll(args, a => a != "--");
                return Array.Exists(zbytek, a => a == "/?" || a == "-?" || a == "-h" || a == "/h")
                    ? ChybaParametru
                    : Spust(zbytek, utcTed, TextWriter.Null);
            }

            var iso = false;
            var zadano = false;
            var casti = new System.Collections.Generic.List<string>();
            foreach (var a in args)
            {
                if (a == "/?" || a == "-?" || a == "-h" || a == "/h") return Napoveda(vystup);
                if (string.Equals(a, "-iso", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/iso", StringComparison.OrdinalIgnoreCase)) iso = true;
                else
                {
                    zadano = true;
                    foreach (var c in a.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        casti.AddRange(c.Split('T'));
                }
            }

            DateTime mistni;
            TimeSpan posun;
            if (!zadano)
            {
                var utc = utcTed();
                posun = PosunUtc(utc);
                mistni = DateTime.SpecifyKind(utc + posun, DateTimeKind.Unspecified);
            }
            else
            {
                if (casti.Count == 0 || casti.Count > 2 || !CtiCas(casti[0], casti.Count == 2 ? casti[1] : null, out mistni))
                {
                    vystup.WriteLine("Chyba: neplatné datum nebo čas: " + string.Join(" ", args));
                    return ChybaParametru;
                }
                if (mistni.Year < PrvniRok)
                {
                    vystup.WriteLine("Chyba: podporovány jsou roky " + PrvniRok + " až 9999.");
                    return ChybaParametru;
                }
                posun = PosunMistni(mistni);
            }

            var text = Text(posun);
            vystup.WriteLine(iso ? mistni.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + text : text);
            return posun.Hours == 2 ? Letni : Zimni;
        }

        public static string Text(TimeSpan posun) => posun.Hours == 2 ? "+02:00" : "+01:00";

        /// <summary>Datum bez času = poledne (v den změny tedy platí posun po změně).</summary>
        public static bool CtiCas(string datum, string cas, out DateTime vysledek)
        {
            vysledek = default;
            if (!DateTime.TryParseExact(datum, FormatyData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return false;
            var t = new TimeSpan(12, 0, 0);
            if (cas != null)
            {
                if (!DateTime.TryParseExact(cas, FormatyCasu, CultureInfo.InvariantCulture, DateTimeStyles.None, out var c))
                    return false;
                t = c.TimeOfDay;
            }
            vysledek = d.Date + t;
            return true;
        }

        /// <summary>Letní čas EU: od poslední neděle v březnu 1:00 UTC do poslední neděle v říjnu 1:00 UTC.</summary>
        public static TimeSpan PosunUtc(DateTime utc)
        {
            var od = PosledniNedele(utc.Year, 3).AddHours(1);
            var doo = PosledniNedele(utc.Year, 10).AddHours(1);
            return utc >= od && utc < doo ? TimeSpan.FromHours(2) : TimeSpan.FromHours(1);
        }

        /// <summary>
        /// Pro český místní čas. Neexistující hodina v březnu (2:00–2:59) i dvakrát se opakující
        /// hodina v říjnu (2:00–2:59) dostanou +02:00.
        /// </summary>
        public static TimeSpan PosunMistni(DateTime mistni)
        {
            var od = PosledniNedele(mistni.Year, 3).AddHours(2);
            var doo = PosledniNedele(mistni.Year, 10).AddHours(3);
            return mistni >= od && mistni < doo ? TimeSpan.FromHours(2) : TimeSpan.FromHours(1);
        }

        public static DateTime PosledniNedele(int rok, int mesic)
        {
            var d = new DateTime(rok, mesic, DateTime.DaysInMonth(rok, mesic));
            return d.AddDays(-(int)d.DayOfWeek);
        }

        private static int Napoveda(TextWriter vystup)
        {
            vystup.WriteLine("TZCZ - posun ceskeho casu proti UTC pro ISO 8601 (+01:00 zimni, +02:00 letni)");
            vystup.WriteLine("Pouziti: TZCZ [--] [-iso] [datum [cas]]");
            vystup.WriteLine("  TZCZ                      posun pro aktualni okamzik");
            vystup.WriteLine("  TZCZ 2036-07-15 10:30     posun pro zadany cesky cas (take 15.7.2036 10:30:00)");
            vystup.WriteLine("  TZCZ -iso 15.7.2036 10:30 vypise 2036-07-15T10:30:00+02:00");
            vystup.WriteLine("  TZCZ -- 2036-07-15 10:30  nic nevypise, vrati jen ERRORLEVEL");
            vystup.WriteLine("Datum bez casu = poledne. ERRORLEVEL: 1 = zimni, 2 = letni, 3 = chyba.");
            return ChybaParametru;
        }
    }
}
