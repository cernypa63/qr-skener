using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Rozeslat
{
    // Poslední použité hodnoty v %APPDATA%\ROZESLAT.
    sealed class Nastaveni
    {
        static readonly string Slozka = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ROZESLAT");
        static string Soubor => Path.Combine(Slozka, "nastaveni.txt");
        static string SouborText => Path.Combine(Slozka, "text.txt");

        public string Csv = "", Sloupec = "", Predmet = "", Text = "";
        public int Pauza = 2;

        public static Nastaveni Nacti()
        {
            var n = new Nastaveni();
            try
            {
                if (File.Exists(Soubor))
                    foreach (var r in File.ReadAllLines(Soubor, Encoding.UTF8))
                    {
                        int i = r.IndexOf('=');
                        if (i < 0) continue;
                        string k = r.Substring(0, i), h = r.Substring(i + 1);
                        if (k == "csv") n.Csv = h;
                        else if (k == "sloupec") n.Sloupec = h;
                        else if (k == "predmet") n.Predmet = h;
                        else if (k == "pauza" && int.TryParse(h, out int p)) n.Pauza = p;
                    }
                if (File.Exists(SouborText)) n.Text = File.ReadAllText(SouborText, Encoding.UTF8);
            }
            catch { }
            return n;
        }

        public void Uloz()
        {
            try
            {
                Directory.CreateDirectory(Slozka);
                File.WriteAllLines(Soubor, new List<string> {
                    "csv=" + Csv, "sloupec=" + Sloupec, "predmet=" + Predmet.Replace("\r", " ").Replace("\n", " "), "pauza=" + Pauza
                }, new UTF8Encoding(false));
                File.WriteAllText(SouborText, Text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
