using System;
using System.IO;
using TzCz;
using Xunit;

namespace TzCz.Tests
{
    public class TzCzTests
    {
        private static readonly TimeZoneInfo Praha = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Central Europe Standard Time" : "Europe/Prague");

        private static (int kod, string text) Spust(DateTime utc, params string[] a)
        {
            var w = new StringWriter();
            var kod = Aplikace.Spust(a, () => utc, w);
            return (kod, w.ToString().TrimEnd());
        }

        [Fact]
        public void PosunUtcOdpovidaSystemuPoHodinach()
        {
            for (var t = new DateTime(1996, 1, 1, 0, 0, 0, DateTimeKind.Utc); t.Year < 2038; t = t.AddMinutes(30))
                Assert.Equal(Praha.GetUtcOffset(t), Aplikace.PosunUtc(t));
        }

        [Fact]
        public void PosunMistniOdpovidaSystemu()
        {
            for (var t = new DateTime(1996, 1, 1); t.Year < 2038; t = t.AddMinutes(30))
            {
                if (Praha.IsInvalidTime(t) || Praha.IsAmbiguousTime(t)) continue;
                Assert.Equal(Praha.GetUtcOffset(t), Aplikace.PosunMistni(t));
            }
        }

        [Theory]
        [InlineData(2026, 3, 29, 1, 59, "+01:00")]
        [InlineData(2026, 3, 29, 2, 30, "+02:00")]
        [InlineData(2026, 3, 29, 3, 0, "+02:00")]
        [InlineData(2026, 10, 25, 1, 59, "+02:00")]
        [InlineData(2026, 10, 25, 2, 30, "+02:00")]
        [InlineData(2026, 10, 25, 3, 0, "+01:00")]
        [InlineData(2100, 3, 28, 3, 0, "+02:00")]
        [InlineData(9999, 12, 31, 23, 59, "+01:00")]
        public void HraniceZmen(int r, int m, int d, int h, int min, string ocekavany) =>
            Assert.Equal(ocekavany, Aplikace.Text(Aplikace.PosunMistni(new DateTime(r, m, d, h, min, 0))));

        [Fact]
        public void PosledniNedele() =>
            Assert.Equal(new DateTime(2036, 3, 30), Aplikace.PosledniNedele(2036, 3));

        [Fact]
        public void AktualniCas()
        {
            Assert.Equal((2, "+02:00"), Spust(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)));
            Assert.Equal((1, "+01:00"), Spust(new DateTime(2026, 11, 25, 15, 0, 0, DateTimeKind.Utc)));
            Assert.Equal((1, "2026-11-25T16:00:00+01:00"), Spust(new DateTime(2026, 11, 25, 15, 0, 0, DateTimeKind.Utc), "-iso"));
            Assert.Equal((2, "2026-10-25T02:30:00+02:00"), Spust(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc), "-ISO"));
            Assert.Equal((1, "2026-10-25T02:30:00+01:00"), Spust(new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc), "-ISO"));
        }

        [Theory]
        [InlineData(2, "+02:00", "2036-07-15", "10:30")]
        [InlineData(2, "+02:00", "2036-07-15 10:30")]
        [InlineData(2, "+02:00", "2036-07-15T10:30:15")]
        [InlineData(1, "+01:00", "15.1.2036", "8:05")]
        [InlineData(1, "+01:00", "01.12.2099")]
        [InlineData(2, "2036-07-15T10:30:00+02:00", "-iso", "15.07.2036", "10:30")]
        [InlineData(1, "2036-01-15T12:00:00+01:00", "/iso", "2036-01-15")]
        [InlineData(1, "2036-01-15T23:59:59+01:00", "2036-01-15", "23:59:59", "-iso")]
        public void ZadanyCas(int kod, string text, params string[] a) =>
            Assert.Equal((kod, text), Spust(DateTime.UtcNow, a));

        [Theory]
        [InlineData("2036-13-01")]
        [InlineData("31.2.2036")]
        [InlineData("2036-07-15", "25:00")]
        [InlineData("2036-07-15", "10:30", "x")]
        [InlineData("1995-07-15")]
        [InlineData("zitra")]
        [InlineData("2036-07-15T")]
        [InlineData("T10:30")]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("-iso", "")]
        public void ChybneParametry(params string[] a)
        {
            var (kod, text) = Spust(DateTime.UtcNow, a);
            Assert.Equal(3, kod);
            Assert.StartsWith("Chyba:", text);
        }

        [Theory]
        [InlineData(2, "--", "2036-07-15", "10:30")]
        [InlineData(1, "2036-01-15", "--")]
        [InlineData(2, "--", "-iso", "2036-07-15")]
        [InlineData(3, "--", "2036-02-30")]
        [InlineData(3, "--", "/?")]
        public void BezVypisu(int kod, params string[] a) =>
            Assert.Equal((kod, ""), Spust(DateTime.UtcNow, a));

        [Fact]
        public void BezVypisuAktualniCas() =>
            Assert.Equal((2, ""), Spust(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), "--"));

        [Fact]
        public void Napoveda() => Assert.Equal(3, Spust(DateTime.UtcNow, "/?").kod);
    }
}
