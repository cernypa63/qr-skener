using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Ascan.Tests
{
    public class PdfTests
    {
        static byte[] FakeJpeg(int width, int height, int components)
        {
            var b = new MemoryStream();
            b.Write(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x4A, 0x46 });
            b.Write(new byte[] { 0xFF, 0xC0, 0x00, 0x0B, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, (byte)components, 0, 0, 0 });
            b.Write(new byte[] { 0xFF, 0xD9 });
            return b.ToArray();
        }

        [Fact]
        public void ReadsJpegSize()
        {
            Assert.True(Pdf.TryReadJpegInfo(FakeJpeg(2480, 3508, 3), out int w, out int h, out int c));
            Assert.Equal((2480, 3508, 3), (w, h, c));
        }

        [Fact]
        public void RejectsNonJpeg() => Assert.False(Pdf.TryReadJpegInfo(new byte[] { 0x42, 0x4D, 0, 0 }, out _, out _, out _));

        [Fact]
        public void A4PageAt300Dpi()
        {
            string pdf = Encoding.Latin1.GetString(Pdf.FromJpegPages(new[] { FakeJpeg(2480, 3508, 1) }, 300, 300));
            Assert.StartsWith("%PDF-1.4", pdf);
            Assert.Contains("/MediaBox [0 0 595.2 841.92]", pdf);
            Assert.Contains("/ColorSpace /DeviceGray", pdf);
            Assert.Contains("/Count 1", pdf);
            Assert.EndsWith("%%EOF\n", pdf);
        }

        [Fact]
        public void XrefOffsetsPointToObjects()
        {
            byte[] bytes = Pdf.FromJpegPages(new[] { FakeJpeg(100, 200, 3), FakeJpeg(300, 100, 3) }, 150, 150);
            string pdf = Encoding.Latin1.GetString(bytes);
            Assert.Contains("/Count 2", pdf);
            int startxref = int.Parse(Regex.Match(pdf, @"startxref\n(\d+)").Groups[1].Value);
            Assert.StartsWith("xref\n0 9\n", pdf.Substring(startxref));
            var entries = Regex.Matches(pdf.Substring(startxref), @"(\d{10}) 00000 n\r\n");
            Assert.Equal(8, entries.Count);
            for (int i = 0; i < entries.Count; i++)
                Assert.StartsWith((i + 1) + " 0 obj\n", pdf.Substring(int.Parse(entries[i].Groups[1].Value)));
        }
    }
}
