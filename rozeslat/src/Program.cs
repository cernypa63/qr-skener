using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Rozeslat
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("/test", StringComparison.OrdinalIgnoreCase))
                return Test(args);

            if (SpustDruhouVerzi(args)) return 0;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HlavniOkno());
            return 0;
        }

        // 32bitový Outlook umí MAPI jen z 32bitového programu, 64bitový jen z 64bitového.
        static bool SpustDruhouVerzi(string[] args)
        {
            try
            {
                var je32 = PostovniProgram.OutlookJe32();
                if (je32 == null) return false;
                string slozka = AppDomain.CurrentDomain.BaseDirectory;
                string druhy = null;
                if (je32 == true && Environment.Is64BitProcess) druhy = Path.Combine(slozka, "ROZESLAT32.exe");
                if (je32 == false && !Environment.Is64BitProcess && Environment.Is64BitOperatingSystem) druhy = Path.Combine(slozka, "ROZESLAT.exe");
                if (druhy == null || !File.Exists(druhy)) return false;
                if (string.Equals(Path.GetFullPath(druhy), Path.GetFullPath(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase)) return false;
                Process.Start(new ProcessStartInfo(druhy, string.Join(" ", args.Select(a => "\"" + a + "\""))) { UseShellExecute = false });
                return true;
            }
            catch { return false; }
        }

        // ROZESLAT.exe /test soubor.csv sloupec předmět text.txt [příloha] – bez okna, výsledek do ROZESLAT_test.txt
        static int Test(string[] a)
        {
            var log = new StringBuilder();
            int chyb = 0;
            try
            {
                var t = SeznamAdres.NactiSoubor(a[1]);
                bool zahlavi = SeznamAdres.MaZahlavi(t);
                int sl = int.Parse(a[2]) - 1;
                if (sl < 0) sl = SeznamAdres.NajdiSloupecSEmaily(t);
                var v = SeznamAdres.Adresy(t, sl, zahlavi);
                log.AppendLine("klient=" + PostovniProgram.Nazev());
                log.AppendLine("bit64=" + Environment.Is64BitProcess);
                log.AppendLine("oddelovac=" + (t.Oddelovac == '\t' ? "TAB" : t.Oddelovac.ToString()));
                log.AppendLine("zahlavi=" + zahlavi + " sloupec=" + (sl + 1));
                log.AppendLine("platne=" + v.Platne.Count + " neplatne=" + v.Neplatne.Count + " duplicity=" + v.Duplicity);
                string text = File.ReadAllText(a[4], Encoding.UTF8);
                var pr = a.Length > 5 ? new List<string> { Path.GetFullPath(a[5]) } : new List<string>();
                if (v.Platne.Count == 0) chyb = 99;
                foreach (var adr in v.Platne)
                {
                    uint k = Mapi.Odesli(adr, a[3], text, pr, false, IntPtr.Zero);
                    log.AppendLine(adr + ";" + k + ";" + Mapi.PopisChyby(k));
                    if (k != 0) chyb++;
                }
            }
            catch (Exception e)
            {
                log.AppendLine("CHYBA " + e);
                chyb = 98;
            }
            File.WriteAllText("ROZESLAT_test.txt", log.ToString(), new UTF8Encoding(false));
            return chyb;
        }
    }
}
