using System;
using System.IO;
using System.Security.Cryptography;

namespace Seet2nd
{
    /// <summary>Vytvoření licenčního kódu soukromým klíčem ze souboru SEET2ND_LIC.key.</summary>
    public static class Podpis
    {
        public const string HlavickaKlice = "SEET2ND-LIC-KEY-1";

        public static ECParameters NactiKlic(string soubor)
        {
            if (!File.Exists(soubor))
                throw new InvalidOperationException("Nenalezen soubor se soukromým klíčem: " + soubor);
            string d = null, x = null, y = null, hlavicka = null;
            foreach (var radek in File.ReadAllLines(soubor))
            {
                var r = radek.Trim();
                if (hlavicka == null) { hlavicka = r; continue; }
                if (r.StartsWith("D=")) d = r.Substring(2);
                else if (r.StartsWith("X=")) x = r.Substring(2);
                else if (r.StartsWith("Y=")) y = r.Substring(2);
            }
            if (hlavicka != HlavickaKlice || d == null || x == null || y == null)
                throw new InvalidOperationException("Soubor " + soubor + " není platný klíč SEET2ND.");
            return new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = Convert.FromBase64String(d),
                Q = new ECPoint { X = Convert.FromBase64String(x), Y = Convert.FromBase64String(y) },
            };
        }

        public static bool PatriKSeet2nd(ECParameters klic) =>
            Convert.ToBase64String(klic.Q.X) == Licence.VerejnyKlicX && Convert.ToBase64String(klic.Q.Y) == Licence.VerejnyKlicY;

        public static string Vytvor(string dic, DateTime? platnaDo, ECParameters klic)
        {
            var platnost = Licence.Platnost(platnaDo);
            using (var ec = ECDsa.Create(klic))
                return platnost + "." + Licence.Base64Url(ec.SignData(Licence.Zprava(dic, platnost), HashAlgorithmName.SHA256));
        }
    }
}
