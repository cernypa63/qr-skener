using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace Seet2nd
{
    /// <summary>Chyba ve vstupním souboru – zapisuje se do SEET2ND.err.</summary>
    public sealed class ChybaVstupu : Exception
    {
        public ChybaVstupu(string zprava) : base(zprava) { }
    }

    public sealed class Vstup
    {
        public string Cert = "";
        public string Pass = "";
        public string Dic = "";
        public bool PlayGround;
        public string IdPokl = "P1";
        public string IdJednotky = "11";
        public string Trzba = "";
        public string Doklad = "";
        public string Output = "";
        public bool PrvniZaslani = true;
        public DateTimeOffset? DatTrzby;
        public int TimeoutSekund = 10;
        public string Licence = "";

        public static Vstup Nacti(string cesta)
        {
            if (!File.Exists(cesta))
                throw new ChybaVstupu("Soubor vstupních hodnot neexistuje: " + cesta);
            var doc = new XmlDocument { XmlResolver = null };
            try
            {
                using (var r = XmlReader.Create(cesta, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                    doc.Load(r);
            }
            catch (Exception e)
            {
                throw new ChybaVstupu("Soubor vstupních hodnot nelze načíst jako XML: " + e.Message);
            }
            return Z(doc);
        }

        public static Vstup Z(XmlDocument doc)
        {
            var root = doc.DocumentElement;
            if (root == null || root.Name != "eet2_data")
                throw new ChybaVstupu("Kořenový prvek musí být <eet2_data>, nalezen <" + root?.Name + ">.");

            string Hodnota(string nazev, bool povinny)
            {
                XmlNode uzel = null;
                foreach (XmlNode n in root.ChildNodes)
                    if (n.NodeType == XmlNodeType.Element && string.Equals(n.Name, nazev, StringComparison.OrdinalIgnoreCase))
                        uzel = n;
                var text = uzel?.InnerText.Trim() ?? "";
                if (povinny && text.Length == 0)
                    throw new ChybaVstupu("Chybí povinný údaj <" + nazev + ">.");
                return text;
            }

            var v = new Vstup
            {
                Output = Hodnota("output", true),
                Cert = Hodnota("cert", true),
                Pass = Hodnota("pass", false),
            };

            var dic = Hodnota("DIC", true);
            v.Dic = NormalizujDic(dic) ?? throw new ChybaVstupu("Neplatné DIČ <DIC>: " + dic + " (očekává se CZ a 8 až 10 číslic).");
            v.Licence = Hodnota("licence", false);

            switch (Hodnota("PlayGround", true))
            {
                case "1": v.PlayGround = true; break;
                case "0": v.PlayGround = false; break;
                default: throw new ChybaVstupu("<PlayGround> musí být 1 (testovací) nebo 0 (ostré prostředí).");
            }

            var idPokl = Hodnota("id_pokl", false);
            if (idPokl.Length > 0) v.IdPokl = idPokl;
            if (!Regex.IsMatch(v.IdPokl, @"^[0-9a-zA-Z\.,:;/#\-_ ]{1,20}$"))
                throw new ChybaVstupu("Neplatné <id_pokl>: " + v.IdPokl + " (1 až 20 znaků: písmena bez diakritiky, číslice, mezera a .,:;/#-_).");

            var idJednotky = Hodnota("id_jednotky", false);
            if (idJednotky.Length > 0) v.IdJednotky = idJednotky;
            if (!Regex.IsMatch(v.IdJednotky, "^[1-9][0-9]{0,8}$"))
                throw new ChybaVstupu("Neplatné <id_jednotky>: " + v.IdJednotky + " (celé číslo 1 až 999999999).");

            var trzba = Hodnota("trzba", true);
            v.Trzba = Castka(trzba) ?? throw new ChybaVstupu("Neplatná částka <trzba>: " + trzba + " (např. 123,12).");

            v.Doklad = Hodnota("doklad", true);
            if (!Regex.IsMatch(v.Doklad, @"^[0-9a-zA-Z\.,:;/#\-_ ]{1,25}$"))
                throw new ChybaVstupu("Neplatné číslo dokladu <doklad>: " + v.Doklad + " (1 až 25 znaků: písmena bez diakritiky, číslice, mezera a .,:;/#-_).");

            switch (Hodnota("prvni_zaslani", false))
            {
                case "": case "1": v.PrvniZaslani = true; break;
                case "0": v.PrvniZaslani = false; break;
                default: throw new ChybaVstupu("<prvni_zaslani> musí být 1 (první odeslání) nebo 0 (opakované odeslání).");
            }

            var datTrzby = Hodnota("dat_trzby", false);
            if (datTrzby.Length > 0)
                v.DatTrzby = Datum(datTrzby) ?? throw new ChybaVstupu("Neplatné <dat_trzby>: " + datTrzby + " (např. 31.10.2026 15:01:03).");
            else if (!v.PrvniZaslani)
                throw new ChybaVstupu("Při opakovaném odeslání (<prvni_zaslani>0) je nutné uvést původní <dat_trzby>.");

            var timeout = Hodnota("timeout", false);
            if (timeout.Length > 0)
            {
                if (!int.TryParse(timeout, NumberStyles.None, CultureInfo.InvariantCulture, out var s) || s < 2 || s > 120)
                    throw new ChybaVstupu("<timeout> musí být počet sekund 2 až 120.");
                v.TimeoutSekund = s;
            }
            return v;
        }

        /// <summary>"cz 12345678" -> "CZ12345678"; null, když text není platné DIČ.</summary>
        public static string NormalizujDic(string text)
        {
            var dic = (text ?? "").Replace(" ", "").Trim().ToUpperInvariant();
            if (!dic.StartsWith("CZ")) dic = "CZ" + dic;
            return Regex.IsMatch(dic, "^CZ[0-9]{8,10}$") ? dic : null;
        }

        /// <summary>"123,5" -> "123.50"; null, když text není platná částka.</summary>
        public static string Castka(string text)
        {
            var t = text.Replace(" ", "").Replace("\u00a0", "").Replace(',', '.');
            if (!Regex.IsMatch(t, @"^-?\d{1,8}(\.\d{0,2})?$")) return null;
            var d = decimal.Parse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            return d.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static readonly string[] FormatySeZonou = { "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ssK" };
        private static readonly string[] FormatyMistni = { "d.M.yyyy H:mm:ss", "d.M.yyyy H:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss" };

        public static DateTimeOffset? Datum(string text)
        {
            var t = text.Trim();
            if (DateTimeOffset.TryParseExact(t, FormatySeZonou, CultureInfo.InvariantCulture, DateTimeStyles.None, out var s))
                return s;
            if (DateTime.TryParseExact(t, FormatyMistni, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var m))
                return new DateTimeOffset(m);
            return null;
        }
    }
}
