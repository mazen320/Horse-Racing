using System.IO;
using System.Text;
using HorseRacing.Registration;
using NUnit.Framework;

namespace HorseRacing.Registration.Tests
{
    public sealed class RegistrationCsvTests
    {
        [Test]
        public void GetCsv_EscapesArabicAndCommas()
        {
            var entry = new RegisterEntry
            {
                userIndex = 1,
                time = "02:30:00 PM 27/08/2026",
                timeUTC = 638910000000000000L,
                name = "محمد",
                email = "test@example.com",
                moblieNumber = "0501234567"
            };

            var csv = entry.GetCsv();
            Assert.That(csv, Does.Contain("محمد"));
            Assert.That(csv, Does.Contain("Player1"));
        }

        [Test]
        public void GetCsv_QuotesNameWithComma()
        {
            var entry = new RegisterEntry
            {
                userIndex = 2,
                time = "02:30:00 PM 27/08/2026",
                timeUTC = 638910000000000000L,
                name = "مازen, Jr",
                email = "",
                moblieNumber = ""
            };

            var csv = entry.GetCsv();
            Assert.That(csv, Does.Contain("\"مازen, Jr\""));
        }

        [Test]
        public void AppendRegistrationRow_WritesUtf8BomOnNewFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "RegistrationCsvTests_" + Path.GetRandomFileName() + ".txt");
            try
            {
                var row = new RegisterEntry
                {
                    userIndex = 1,
                    time = "02:30:00 PM 27/08/2026",
                    timeUTC = 638910000000000000L,
                    name = "محمد",
                    email = "",
                    moblieNumber = ""
                }.GetCsv();

                RegistrationCsvUtil.AppendRegistrationRow(path, row);

                var bytes = File.ReadAllBytes(path);
                Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(3));
                Assert.That(bytes[0], Is.EqualTo(0xEF));
                Assert.That(bytes[1], Is.EqualTo(0xBB));
                Assert.That(bytes[2], Is.EqualTo(0xBF));

                var text = File.ReadAllText(path, new UTF8Encoding(true));
                Assert.That(text, Does.Contain("محمد"));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void AppendRegistrationRow_AppendDoesNotDuplicateBom()
        {
            var path = Path.Combine(Path.GetTempPath(), "RegistrationCsvTests_" + Path.GetRandomFileName() + ".txt");
            try
            {
                RegistrationCsvUtil.AppendRegistrationRow(path, "row1");
                RegistrationCsvUtil.AppendRegistrationRow(path, "row2");

                var bytes = File.ReadAllBytes(path);
                var bomCount = 0;
                for (var i = 0; i <= bytes.Length - 3; i++)
                {
                    if (bytes[i] == 0xEF && bytes[i + 1] == 0xBB && bytes[i + 2] == 0xBF)
                        bomCount++;
                }

                Assert.That(bomCount, Is.EqualTo(1));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
