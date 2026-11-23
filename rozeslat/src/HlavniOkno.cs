using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Rozeslat
{
    sealed class HlavniOkno : Form
    {
        readonly TextBox tCsv = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        readonly Button bCsv = new Button { Text = "Vybrat CSV...", AutoSize = true };
        readonly ComboBox cSloupec = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly CheckBox chZahlavi = new CheckBox { Text = "První řádek je záhlaví", AutoSize = true };
        readonly Label lAdresy = new Label { AutoSize = true, Text = "Vyberte soubor CSV." };
        readonly LinkLabel lnZobraz = new LinkLabel { Text = "Zobrazit adresy", AutoSize = true, Enabled = false };
        readonly TextBox tPredmet = new TextBox { Dock = DockStyle.Fill };
        readonly TextBox tText = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, Dock = DockStyle.Fill, WordWrap = true };
        readonly ListBox lbPrilohy = new ListBox { Dock = DockStyle.Fill, Height = 50, HorizontalScrollbar = true };
        readonly Button bPridat = new Button { Text = "Přidat...", AutoSize = true };
        readonly Button bOdebrat = new Button { Text = "Odebrat", AutoSize = true };
        readonly NumericUpDown nPauza = new NumericUpDown { Minimum = 0, Maximum = 600, Value = 2, Width = 60 };
        readonly Button bZkusebni = new Button { Text = "Zkušební e-mail...", AutoSize = true };
        readonly Button bOdeslat = new Button { Text = "Odeslat všem", AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        readonly Button bZastavit = new Button { Text = "Zastavit", AutoSize = true, Enabled = false };
        readonly ProgressBar pb = new ProgressBar { Dock = DockStyle.Fill, Height = 18 };
        readonly Label lStav = new Label { AutoSize = true, Dock = DockStyle.Fill };

        Tabulka tabulka;
        VysledekAdres adresy = new VysledekAdres();
        readonly HashSet<string> odeslano = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Nastaveni nast = Nastaveni.Nacti();
        volatile bool zastavit;
        bool odesila;

        public HlavniOkno()
        {
            Text = "ROZESLAT – hromadné rozeslání e-mailu";
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 600);
            MinimumSize = new Size(560, 480);

            var g = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(10) };
            g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Radek(g, "Soubor CSV:", tCsv, bCsv);
            Radek(g, "Sloupec s e-maily:", cSloupec, chZahlavi);
            var pa = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            pa.Controls.Add(lAdresy); pa.Controls.Add(lnZobraz);
            Radek(g, "", pa, null);
            Radek(g, "Předmět:", tPredmet, null);
            Radek(g, "Text e-mailu:", tText, null, true);
            var pp = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = Padding.Empty };
            pp.Controls.Add(bPridat); pp.Controls.Add(bOdebrat);
            Radek(g, "Přílohy:", lbPrilohy, pp);
            var pz = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            pz.Controls.Add(nPauza);
            pz.Controls.Add(new Label { Text = "vteřin pauza mezi e-maily", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            Radek(g, "Pauza:", pz, null);
            var pt = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 8, 0, 4) };
            pt.Controls.Add(bOdeslat); pt.Controls.Add(bZastavit); pt.Controls.Add(bZkusebni);
            Radek(g, "", pt, null);
            Radek(g, "", pb, null);
            Radek(g, "", lStav, null);
            Controls.Add(g);

            bCsv.Click += (s, e) => VyberCsv();
            cSloupec.SelectedIndexChanged += (s, e) => Prepocti();
            chZahlavi.CheckedChanged += (s, e) => { if (tabulka != null) NaplnSloupce(cSloupec.SelectedIndex); };
            lnZobraz.LinkClicked += (s, e) => ZobrazAdresy();
            bPridat.Click += (s, e) => PridejPrilohy();
            bOdebrat.Click += (s, e) => { foreach (var o in lbPrilohy.SelectedItems.Cast<object>().ToList()) lbPrilohy.Items.Remove(o); };
            bOdeslat.Click += (s, e) => OdeslatVsem();
            bZastavit.Click += (s, e) => { zastavit = true; bZastavit.Enabled = false; lStav.Text = "Zastavuji po dokončení aktuálního e-mailu..."; };
            bZkusebni.Click += (s, e) => Zkusebni();
            foreach (var tb in new[] { tPredmet, tText })
                tb.KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.A) { ((TextBox)s).SelectAll(); e.SuppressKeyPress = true; } };
            FormClosing += Zavirani;

            string klient = PostovniProgram.Nazev();
            lStav.Text = klient.Length > 0 ? "Výchozí poštovní program: " + klient : "Ve Windows není nastavený výchozí poštovní program.";

            tPredmet.Text = nast.Predmet;
            tText.Text = nast.Text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            nPauza.Value = Math.Max(nPauza.Minimum, Math.Min(nPauza.Maximum, nast.Pauza));
            if (nast.Csv.Length > 0 && File.Exists(nast.Csv)) NactiCsv(nast.Csv, nast.Sloupec);
        }

        static void Radek(TableLayoutPanel g, string popis, Control stred, Control vpravo, bool roztahnout = false)
        {
            int r = g.RowCount++;
            g.RowStyles.Add(roztahnout ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            g.Controls.Add(new Label { Text = popis, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Padding = new Padding(0, 6, 6, 0) }, 0, r);
            if (vpravo == null) { g.Controls.Add(stred, 1, r); g.SetColumnSpan(stred, 2); }
            else { g.Controls.Add(stred, 1, r); g.Controls.Add(vpravo, 2, r); }
        }

        void VyberCsv()
        {
            using (var d = new OpenFileDialog { Filter = "Soubory CSV (*.csv;*.txt)|*.csv;*.txt|Všechny soubory (*.*)|*.*", Title = "Vyberte soubor s e-mailovými adresami" })
            {
                if (tCsv.Text.Length > 0) try { d.InitialDirectory = Path.GetDirectoryName(tCsv.Text); } catch { }
                if (d.ShowDialog(this) == DialogResult.OK) NactiCsv(d.FileName, null);
            }
        }

        void NactiCsv(string cesta, string sloupec)
        {
            try { tabulka = SeznamAdres.NactiSoubor(cesta); }
            catch (Exception e)
            {
                MessageBox.Show(this, "Soubor se nepodařilo načíst:\n" + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            tCsv.Text = cesta;
            odeslano.Clear();
            chZahlavi.Checked = SeznamAdres.MaZahlavi(tabulka);
            int vyber = SeznamAdres.NajdiSloupecSEmaily(tabulka);
            if (!string.IsNullOrEmpty(sloupec) && int.TryParse(sloupec, out int ul) && ul < tabulka.PocetSloupcu) vyber = ul;
            NaplnSloupce(vyber);
            if (tabulka.PocetSloupcu > 1 && sloupec == null)
            {
                cSloupec.DroppedDown = false;
                lStav.Text = "Zkontrolujte, zda je vybraný správný sloupec s e-maily.";
                cSloupec.Focus();
            }
        }

        void NaplnSloupce(int vyber)
        {
            cSloupec.Items.Clear();
            foreach (var n in SeznamAdres.NazvySloupcu(tabulka, chZahlavi.Checked)) cSloupec.Items.Add(n);
            if (cSloupec.Items.Count > 0) cSloupec.SelectedIndex = Math.Max(0, Math.Min(vyber, cSloupec.Items.Count - 1));
            Prepocti();
        }

        void Prepocti()
        {
            if (tabulka == null || cSloupec.SelectedIndex < 0) { adresy = new VysledekAdres(); lAdresy.Text = "Vyberte soubor CSV."; lnZobraz.Enabled = false; return; }
            adresy = SeznamAdres.Adresy(tabulka, cSloupec.SelectedIndex, chZahlavi.Checked);
            var t = "Nalezeno adres: " + adresy.Platne.Count;
            var dal = new List<string>();
            if (adresy.Duplicity > 0) dal.Add("duplicitních vynecháno " + adresy.Duplicity);
            if (adresy.Neplatne.Count > 0) dal.Add("neplatných " + adresy.Neplatne.Count);
            if (dal.Count > 0) t += " (" + string.Join(", ", dal) + ")";
            lAdresy.Text = t;
            lAdresy.ForeColor = adresy.Platne.Count == 0 ? Color.Firebrick : SystemColors.ControlText;
            lnZobraz.Enabled = adresy.Platne.Count + adresy.Neplatne.Count > 0;
        }

        void ZobrazAdresy()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ADRESY K ODESLÁNÍ (" + adresy.Platne.Count + "):");
            foreach (var a in adresy.Platne) sb.AppendLine((odeslano.Contains(a) ? "[odesláno] " : "") + a);
            if (adresy.Neplatne.Count > 0)
            {
                sb.AppendLine().AppendLine("NEPLATNÉ – NEODEŠLOU SE (" + adresy.Neplatne.Count + "):");
                foreach (var a in adresy.Neplatne) sb.AppendLine(a);
            }
            using (var f = new Form { Text = "Adresy", Size = new Size(520, 500), StartPosition = FormStartPosition.CenterParent, Font = Font, MinimizeBox = false })
            {
                f.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Text = sb.ToString(), WordWrap = false });
                f.ShowDialog(this);
            }
        }

        void PridejPrilohy()
        {
            using (var d = new OpenFileDialog { Multiselect = true, Title = "Vyberte přílohy" })
                if (d.ShowDialog(this) == DialogResult.OK)
                    foreach (var f in d.FileNames) if (!lbPrilohy.Items.Contains(f)) lbPrilohy.Items.Add(f);
        }

        List<string> Prilohy() => lbPrilohy.Items.Cast<string>().ToList();

        bool Zkontroluj()
        {
            if (PostovniProgram.Nazev().Length == 0 &&
                MessageBox.Show(this, "Ve Windows není nastavený výchozí poštovní program. Odeslání pravděpodobně selže.\n\nPokračovat?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return false;
            if (tPredmet.Text.Trim().Length == 0 &&
                MessageBox.Show(this, "Předmět je prázdný. Pokračovat?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return false;
            if (tText.Text.Trim().Length == 0 &&
                MessageBox.Show(this, "Text e-mailu je prázdný. Pokračovat?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return false;
            foreach (var p in Prilohy())
                if (!File.Exists(p)) { MessageBox.Show(this, "Příloha neexistuje:\n" + p, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
            return true;
        }

        void Zkusebni()
        {
            string adr = Zeptej("Na jakou adresu poslat zkušební e-mail?");
            if (adr == null) return;
            if (!SeznamAdres.JePlatnaAdresa(adr)) { MessageBox.Show(this, "Neplatná adresa: " + adr, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (!Zkontroluj()) return;
            Spust(new List<string> { adr.Trim() }, true);
        }

        string Zeptej(string otazka)
        {
            using (var f = new Form { Text = Text, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, Font = Font, ClientSize = new Size(380, 110) })
            {
                var t = new TextBox { Left = 12, Top = 34, Width = 356 };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 212, Top = 70, Width = 75 };
                var st = new Button { Text = "Storno", DialogResult = DialogResult.Cancel, Left = 293, Top = 70, Width = 75 };
                f.Controls.Add(new Label { Text = otazka, Left = 12, Top = 12, AutoSize = true });
                f.Controls.Add(t); f.Controls.Add(ok); f.Controls.Add(st);
                f.AcceptButton = ok; f.CancelButton = st;
                return f.ShowDialog(this) == DialogResult.OK && t.Text.Trim().Length > 0 ? t.Text.Trim() : null;
            }
        }

        void OdeslatVsem()
        {
            if (adresy.Platne.Count == 0) { MessageBox.Show(this, "Nejsou žádné adresy k odeslání. Vyberte soubor CSV a sloupec s e-maily.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (!Zkontroluj()) return;
            var seznam = adresy.Platne.ToList();
            int uz = seznam.Count(a => odeslano.Contains(a));
            if (uz > 0)
            {
                var r = MessageBox.Show(this, "Na " + uz + " z " + seznam.Count + " adres už byl e-mail v tomto okně odeslán.\n\nAno = poslat jen zbývajícím\nNe = poslat znovu všem", Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                if (r == DialogResult.Yes) seznam = seznam.Where(a => !odeslano.Contains(a)).ToList();
                if (seznam.Count == 0) return;
            }
            string klient = PostovniProgram.Nazev();
            if (MessageBox.Show(this, "Odeslat e-mail „" + tPredmet.Text.Trim() + "“ na " + seznam.Count + " adres" + (klient.Length > 0 ? " přes " + klient : "") + "?\n\nKaždý adresát dostane samostatný e-mail a ostatní adresy neuvidí.",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Spust(seznam, false);
        }

        void Spust(List<string> seznam, bool zkusebni)
        {
            string predmet = tPredmet.Text.Trim(), text = tText.Text;
            var prilohy = Prilohy();
            int pauza = (int)nPauza.Value;
            string log = tCsv.Text.Length > 0 && !zkusebni ? Path.Combine(Path.GetDirectoryName(tCsv.Text), Path.GetFileNameWithoutExtension(tCsv.Text) + "_odeslano.csv") : null;
            zastavit = false;
            NastavOdesilani(true);
            pb.Maximum = seznam.Count; pb.Value = 0;
            var vl = new Thread(() =>
            {
                int ok = 0, chyb = 0;
                var chyby = new List<string>();
                for (int i = 0; i < seznam.Count && !zastavit; i++)
                {
                    string a = seznam[i];
                    Ui(() => lStav.Text = "Odesílám " + (i + 1) + " z " + seznam.Count + ": " + a);
                    uint k;
                    try { k = Mapi.Odesli(a, predmet, text, prilohy, false, IntPtr.Zero); }
                    catch (DllNotFoundException) { k = 9999; }
                    catch (Exception) { k = 9998; }
                    string popis = k == 9999 ? "v počítači chybí rozhraní MAPI" : k == 9998 ? "neočekávaná chyba" : Mapi.PopisChyby(k);
                    if (k == 0) { ok++; Ui(() => odeslano.Add(a)); }
                    else { chyb++; chyby.Add(a + ": " + popis); }
                    if (log != null)
                        try { File.AppendAllText(log, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + ";" + a + ";" + popis + "\r\n", new UTF8Encoding(true)); } catch { }
                    Ui(() => pb.Value = i + 1);
                    if (k == 1 || k == 3 || k >= 9998) { zastavit = true; break; }
                    for (int s = 0; s < pauza * 10 && !zastavit && i < seznam.Count - 1; s++) Thread.Sleep(100);
                }
                Ui(() => Hotovo(ok, chyb, chyby, seznam.Count, log));
            });
            vl.SetApartmentState(ApartmentState.STA);
            vl.IsBackground = true;
            vl.Start();
        }

        void Ui(Action a) { try { BeginInvoke(a); } catch { } }

        void Hotovo(int ok, int chyb, List<string> chyby, int celkem, string log)
        {
            NastavOdesilani(false);
            var sb = new StringBuilder();
            sb.AppendLine("Předáno poštovnímu programu k odeslání: " + ok + " z " + celkem);
            if (chyb > 0) sb.AppendLine("Neodesláno: " + chyb);
            if (ok + chyb < celkem) sb.AppendLine("Zastaveno, neodesláno zbývajícím: " + (celkem - ok - chyb));
            foreach (var c in chyby.Take(15)) sb.AppendLine("  " + c);
            if (chyby.Count > 15) sb.AppendLine("  ...");
            if (log != null) sb.AppendLine().AppendLine("Záznam: " + log);
            sb.AppendLine().Append("E-maily najdete v poštovním programu v Odeslané poště (nebo chvíli v Pošta k odeslání).");
            lStav.Text = "Hotovo: odesláno " + ok + " z " + celkem + ".";
            Prepocti();
            MessageBox.Show(this, sb.ToString(), Text, MessageBoxButtons.OK, chyb > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        void NastavOdesilani(bool ano)
        {
            odesila = ano;
            foreach (Control c in new Control[] { bCsv, cSloupec, chZahlavi, tPredmet, tText, bPridat, bOdebrat, nPauza, bOdeslat, bZkusebni })
                c.Enabled = !ano;
            bZastavit.Enabled = ano;
        }

        void Zavirani(object s, FormClosingEventArgs e)
        {
            if (odesila)
            {
                if (MessageBox.Show(this, "Probíhá odesílání. Opravdu zavřít?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) { e.Cancel = true; return; }
                zastavit = true;
            }
            nast.Csv = tCsv.Text;
            nast.Sloupec = cSloupec.SelectedIndex >= 0 ? cSloupec.SelectedIndex.ToString() : "";
            nast.Predmet = tPredmet.Text;
            nast.Text = tText.Text;
            nast.Pauza = (int)nPauza.Value;
            nast.Uloz();
        }
    }
}
