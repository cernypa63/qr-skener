using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace Seet2nd
{
    /// <summary>
    /// Generátor licencí. Bez parametrů okno, s parametry příkazový řádek:
    /// SEET2ND_LIC.exe DIČ [platnost d.M.yyyy nebo 0 = bez omezení] [soubor pro kód]
    /// Návratový kód: 0 = OK, 2 = chybné DIČ/datum, 3 = chybí nebo nesedí klíč SEET2ND_LIC.key.
    /// </summary>
    internal static class Program
    {
        public const string SouborKlice = "SEET2ND_LIC.key";
        public const string SouborSeznamu = "licence_seznam.csv";

        public static string Adresar => AppDomain.CurrentDomain.BaseDirectory;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0) return PrikazovyRadek(args);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Okno());
            return 0;
        }

        public static ECParameters Klic()
        {
            var klic = Podpis.NactiKlic(Path.Combine(Adresar, SouborKlice));
            if (!Podpis.PatriKSeet2nd(klic))
                throw new InvalidOperationException("Klíč " + SouborKlice + " nepatří k této verzi SEET2ND.exe.");
            return klic;
        }

        public static bool Datum(string text, out DateTime? platnaDo)
        {
            platnaDo = null;
            if (text.Trim() == "0") return true;
            if (!DateTime.TryParseExact(text.Trim(), new[] { "d.M.yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                || d < DateTime.Today) return false;
            platnaDo = d;
            return true;
        }

        public static string Vytvor(ECParameters klic, string dic, DateTime? platnaDo, string firma)
        {
            var kod = Podpis.Vytvor(dic, platnaDo, klic);
            if (!Licence.Over(dic, kod, DateTime.Today).Platna)
                throw new InvalidOperationException("Vytvořený kód nejde ověřit.");
            try
            {
                var seznam = Path.Combine(Adresar, SouborSeznamu);
                var novy = !File.Exists(seznam);
                var platnost = platnaDo?.ToString("d.M.yyyy", CultureInfo.InvariantCulture) ?? "bez omezení";
                File.AppendAllText(seznam,
                    (novy ? "vydáno;DIČ;firma;platnost;kód" + Environment.NewLine : "") +
                    DateTime.Now.ToString("d.M.yyyy HH:mm", CultureInfo.InvariantCulture) + ";" + dic + ";" +
                    (firma ?? "").Replace(";", ",") + ";" + platnost + ";" + kod + Environment.NewLine,
                    Encoding.GetEncoding(1250));
            }
            catch (Exception) { }
            return kod;
        }

        private static int PrikazovyRadek(string[] args)
        {
            var dic = Vstup.NormalizujDic(args[0]);
            DateTime? platnaDo = null;
            if (dic == null || (args.Length > 1 && !Datum(args[1], out platnaDo)))
            {
                Console.Error.WriteLine("Použití: SEET2ND_LIC.exe DIČ [platnost d.M.yyyy nebo 0] [soubor]");
                return 2;
            }
            ECParameters klic;
            try { klic = Klic(); }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                if (args.Length > 2) File.WriteAllText(args[2], "CHYBA: " + e.Message, Encoding.GetEncoding(1250));
                return 3;
            }
            var kod = Vytvor(klic, dic, platnaDo, "");
            if (args.Length > 2) File.WriteAllText(args[2], kod, Encoding.ASCII);
            else Console.WriteLine(kod);
            return 0;
        }
    }

    internal sealed class Okno : Form
    {
        private readonly TextBox _dic = new TextBox();
        private readonly TextBox _firma = new TextBox();
        private readonly CheckBox _bezOmezeni = new CheckBox { Text = "bez omezení" };
        private readonly DateTimePicker _platnost = new DateTimePicker { Format = DateTimePickerFormat.Short };
        private readonly Button _vytvor = new Button { Text = "Vytvořit licenci" };
        private readonly TextBox _vysledek = new TextBox { ReadOnly = true, Multiline = true };
        private readonly Button _kopiruj = new Button { Text = "Kopírovat", Enabled = false };
        private readonly Label _stav = new Label { AutoSize = false };
        private ECParameters _klic;

        public Okno()
        {
            Text = "SEET2ND – generátor licencí";
            Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 330);

            Pridej(new Label { Text = "DIČ:", AutoSize = true }, 16, 20);
            Pridej(_dic, 140, 16, 200);
            Pridej(new Label { Text = "Firma (poznámka):", AutoSize = true }, 16, 56);
            Pridej(_firma, 140, 52, 400);
            Pridej(new Label { Text = "Platnost do:", AutoSize = true }, 16, 92);
            Pridej(_platnost, 140, 88, 140);
            Pridej(_bezOmezeni, 300, 90, 160);
            Pridej(_vytvor, 140, 128, 180, 34);
            Pridej(new Label { Text = "Do vstupního XML:", AutoSize = true }, 16, 178);
            Pridej(_vysledek, 16, 200, 528, 60);
            Pridej(_kopiruj, 16, 270, 120, 34);
            Pridej(_stav, 150, 276, 394, 40);

            _platnost.Value = DateTime.Today.AddYears(1);
            _platnost.MinDate = DateTime.Today;
            _bezOmezeni.CheckedChanged += (s, e) => _platnost.Enabled = !_bezOmezeni.Checked;
            _vytvor.Click += (s, e) => Vytvor();
            _kopiruj.Click += (s, e) => { Clipboard.SetText(_vysledek.Text); _stav.Text = "Zkopírováno do schránky."; };
            AcceptButton = _vytvor;

            try { _klic = Program.Klic(); }
            catch (Exception e)
            {
                _vytvor.Enabled = false;
                _stav.ForeColor = Color.DarkRed;
                _stav.Text = "Licence nejde vytvářet.";
                Shown += (s, a) => MessageBox.Show(this, e.Message + Environment.NewLine + Environment.NewLine +
                    "Soubor " + Program.SouborKlice + " musí být ve stejné složce jako SEET2ND_LIC.exe.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Pridej(Control c, int x, int y, int sirka = 0, int vyska = 0)
        {
            c.Location = new Point(x, y);
            if (sirka > 0) c.Width = sirka;
            if (vyska > 0) c.Height = vyska;
            Controls.Add(c);
        }

        private void Vytvor()
        {
            var dic = Vstup.NormalizujDic(_dic.Text);
            if (dic == null)
            {
                MessageBox.Show(this, "Neplatné DIČ (CZ a 8 až 10 číslic).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dic.Focus();
                return;
            }
            _dic.Text = dic;
            DateTime? platnaDo = _bezOmezeni.Checked ? (DateTime?)null : _platnost.Value.Date;
            var kod = Program.Vytvor(_klic, dic, platnaDo, _firma.Text.Trim());
            _vysledek.Text = "<licence>" + kod + "</licence>";
            _kopiruj.Enabled = true;
            _stav.ForeColor = Color.DarkGreen;
            _stav.Text = "Licence pro " + dic + " vytvořena, zapsána do " + Program.SouborSeznamu + ".";
        }
    }
}
