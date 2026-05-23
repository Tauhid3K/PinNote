using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace PinNote.UI
{
    public class StripTagsConverter : IValueConverter
    {
        private static readonly Regex TagRegex = new("<.*?>", RegexOptions.Compiled);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var s = value.ToString();
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            var richTextPreview = TryReadXamlText(s);
            if (!string.IsNullOrWhiteSpace(richTextPreview))
            {
                return NormalizePreview(richTextPreview);
            }

            var stripped = TagRegex.Replace(s, string.Empty);
            return NormalizePreview(WebUtility.HtmlDecode(stripped));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static string TryReadXamlText(string value)
        {
            try
            {
                var document = new FlowDocument();
                var range = new TextRange(document.ContentStart, document.ContentEnd);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(value));
                range.Load(stream, DataFormats.Xaml);
                return range.Text;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizePreview(string value)
        {
            return Regex.Replace(value, @"\s+", " ").Trim();
        }
    }
}
