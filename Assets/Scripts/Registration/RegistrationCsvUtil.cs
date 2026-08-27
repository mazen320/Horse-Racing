using System.IO;
using System.Text;

namespace HorseRacing.Registration
{
    public static class RegistrationCsvUtil
    {
        static readonly UTF8Encoding Utf8WithBom = new(true);
        static readonly UTF8Encoding Utf8NoBom = new(false);

        public static string EscapeField(string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        /// <summary>UTF-8 with BOM on first write; plain UTF-8 append after (Excel-friendly Arabic).</summary>
        public static void AppendRegistrationRow(string path, string csvRow)
        {
            var line = csvRow + System.Environment.NewLine;
            if (!File.Exists(path))
            {
                File.WriteAllText(path, line, Utf8WithBom);
                return;
            }

            File.AppendAllText(path, line, Utf8NoBom);
        }
    }
}
