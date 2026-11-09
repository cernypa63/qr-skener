using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Seet2nd
{
    /// <summary>
    /// Licence pro ostré prostředí EET: kód je podpis (ECDSA P-256) DIČ a data platnosti.
    /// Tvar kódu: "RRRRMMDD.podpis" nebo "0.podpis" (bez omezení). Ověřuje se veřejným klíčem,
    /// soukromý klíč má jen generátor SEET2ND_LIC.exe.
    /// </summary>
    public static class Licence
    {
        public const string VerejnyKlicX = "h1FIIwcNvxWfAlLdkaltuPIl2VAMOxsB8VvdY9OTHmQ=";
        public const string VerejnyKlicY = "RySFiQ56rDbsaLYD3sBXlYfW0bggOIktzHNd6lE9nMY=";
        public const string BezOmezeni = "0";

        public sealed class Vysledek
        {
            public bool Platna;
            public string Chyba = "";
            /// <summary>null = bez omezení.</summary>
            public DateTime? PlatnaDo;
            public string PlatnostText => PlatnaDo?.ToString("d.M.yyyy", CultureInfo.InvariantCulture) ?? "bez omezení";
        }

        public static byte[] Zprava(string dic, string platnost) => Encoding.UTF8.GetBytes("SEET2ND|1|" + dic + "|" + platnost);

        public static string Platnost(DateTime? platnaDo) =>
            platnaDo?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? BezOmezeni;

        public static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] ZBase64Url(string s)
        {
            var t = s.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(t + new string('=', (4 - t.Length % 4) % 4));
        }

        public static ECParameters VerejnyKlic() => new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Convert.FromBase64String(VerejnyKlicX), Y = Convert.FromBase64String(VerejnyKlicY) },
        };

        public static Vysledek Over(string dic, string kod, DateTime dnes) => Over(dic, kod, dnes, VerejnyKlic());

        public static Vysledek Over(string dic, string kod, DateTime dnes, ECParameters klic)
        {
            var v = new Vysledek();
            kod = (kod ?? "").Trim();
            if (kod.Length == 0)
            {
                v.Chyba = "Chybí licence pro ostré prostředí (<licence>) pro DIČ " + dic + ". Bez licence lze odesílat jen do testovacího prostředí (<PlayGround>1).";
                return v;
            }
            var tecka = kod.IndexOf('.');
            byte[] podpis = null;
            var platnost = tecka > 0 ? kod.Substring(0, tecka) : "";
            if (tecka > 0)
            {
                try { podpis = ZBase64Url(kod.Substring(tecka + 1)); } catch (FormatException) { }
            }
            DateTime datum = default;
            var datumOk = platnost == BezOmezeni ||
                DateTime.TryParseExact(platnost, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out datum);
            bool podpisOk = false;
            if (podpis != null && podpis.Length == 64 && datumOk)
            {
                using (var ec = ECDsa.Create(klic))
                    podpisOk = ec.VerifyData(Zprava(dic, platnost), podpis, HashAlgorithmName.SHA256);
            }
            if (!podpisOk)
            {
                v.Chyba = "Neplatný licenční kód pro DIČ " + dic + " (kód nepatří k tomuto DIČ nebo je chybně opsaný).";
                return v;
            }
            if (platnost != BezOmezeni)
            {
                v.PlatnaDo = datum;
                if (dnes.Date > datum)
                {
                    v.Chyba = "Licence pro DIČ " + dic + " skončila " + v.PlatnostText + ".";
                    return v;
                }
            }
            v.Platna = true;
            return v;
        }
    }
}
