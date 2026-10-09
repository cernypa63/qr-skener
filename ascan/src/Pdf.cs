using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ascan
{
    /// <summary>Minimal PDF writer: one page per JPEG image, embedded as-is (DCTDecode).</summary>
    static class Pdf
    {
        public static byte[] FromJpegPages(IList<byte[]> jpegs, double dpiX, double dpiY)
        {
            if (jpegs.Count == 0) throw new ArgumentException("Žádná stránka.");
            if (dpiX <= 0) dpiX = 300;
            if (dpiY <= 0) dpiY = dpiX;

            var ms = new MemoryStream();
            var offsets = new List<long>();
            Write(ms, "%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");

            int pageCount = jpegs.Count;
            var kids = new StringBuilder();
            for (int i = 0; i < pageCount; i++) kids.Append(3 + i * 3).Append(" 0 R ");

            BeginObject(ms, offsets, 1);
            Write(ms, "<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            BeginObject(ms, offsets, 2);
            Write(ms, "<< /Type /Pages /Kids [ " + kids + "] /Count " + pageCount + " >>\nendobj\n");

            for (int i = 0; i < pageCount; i++)
            {
                byte[] jpeg = jpegs[i];
                if (!TryReadJpegInfo(jpeg, out int w, out int h, out int components))
                    throw new InvalidDataException("Naskenovaný obrázek není platný JPEG.");
                string colorSpace = components == 1 ? "/DeviceGray" : components == 4 ? "/DeviceCMYK" : "/DeviceRGB";
                string widthPt = Num(w * 72.0 / dpiX);
                string heightPt = Num(h * 72.0 / dpiY);
                int pageObj = 3 + i * 3, imageObj = pageObj + 1, contentObj = pageObj + 2;

                BeginObject(ms, offsets, pageObj);
                Write(ms, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + widthPt + " " + heightPt + "]"
                    + " /Resources << /XObject << /Im0 " + imageObj + " 0 R >> >> /Contents " + contentObj + " 0 R >>\nendobj\n");

                BeginObject(ms, offsets, imageObj);
                Write(ms, "<< /Type /XObject /Subtype /Image /Width " + w + " /Height " + h
                    + " /ColorSpace " + colorSpace + " /BitsPerComponent 8 /Filter /DCTDecode /Length " + jpeg.Length + " >>\nstream\n");
                ms.Write(jpeg, 0, jpeg.Length);
                Write(ms, "\nendstream\nendobj\n");

                string content = "q " + widthPt + " 0 0 " + heightPt + " 0 0 cm /Im0 Do Q";
                BeginObject(ms, offsets, contentObj);
                Write(ms, "<< /Length " + content.Length + " >>\nstream\n" + content + "\nendstream\nendobj\n");
            }

            long xref = ms.Position;
            var sb = new StringBuilder();
            sb.Append("xref\n0 ").Append(offsets.Count + 1).Append('\n');
            sb.Append("0000000000 65535 f\r\n");
            foreach (long o in offsets) sb.Append(o.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n\r\n");
            sb.Append("trailer\n<< /Size ").Append(offsets.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n")
              .Append(xref.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
            Write(ms, sb.ToString());
            return ms.ToArray();
        }

        public static bool TryReadJpegInfo(byte[] d, out int width, out int height, out int components)
        {
            width = height = components = 0;
            if (d == null || d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) return false;
            int p = 2;
            while (p + 3 < d.Length)
            {
                if (d[p] != 0xFF) return false;
                while (p < d.Length && d[p] == 0xFF) p++;
                if (p >= d.Length) return false;
                byte marker = d[p];
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { p++; continue; }
                if (marker == 0xD9 || marker == 0xDA) return false;
                if (p + 2 >= d.Length) return false;
                int length = (d[p + 1] << 8) | d[p + 2];
                bool isSof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (isSof)
                {
                    if (p + 8 >= d.Length) return false;
                    height = (d[p + 4] << 8) | d[p + 5];
                    width = (d[p + 6] << 8) | d[p + 7];
                    components = d[p + 8];
                    return width > 0 && height > 0;
                }
                p += 1 + length;
            }
            return false;
        }

        static void BeginObject(MemoryStream ms, List<long> offsets, int number)
        {
            offsets.Add(ms.Position);
            Write(ms, number + " 0 obj\n");
        }

        static void Write(MemoryStream ms, string s)
        {
            byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(s);
            ms.Write(bytes, 0, bytes.Length);
        }

        static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
