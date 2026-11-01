using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using PinNote.Models;

namespace PinNote.Services
{
    public static class SearchService
    {
        private static readonly Regex TagRegex = new("<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex LineBreakRegex = new(@"<LineBreak\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex BlockEndRegex = new(@"</(Paragraph|ListItem|BlockUIContainer)\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

        public static string ExtractSearchableText(string? rawContent, NoteType noteType, IEnumerable<ChecklistItemModel>? checklistItems)
        {
            if (noteType == NoteType.Checklist && checklistItems != null)
            {
                var sb = new StringBuilder();
                foreach (var item in checklistItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Text)) continue;
                    sb.Append(item.IsChecked ? "[x] " : "[ ] ");
                    sb.AppendLine(item.Text);
                }
                return sb.ToString().Trim();
            }

            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return string.Empty;
            }

            // If it looks like XAML/XML (e.g. FlowDocument)
            if (rawContent.Contains('<') && rawContent.Contains('>'))
            {
                // Try XML parsing first for clean text extraction without tag leakage
                try
                {
                    var xmlSettings = new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Ignore,
                        IgnoreComments = true,
                        IgnoreProcessingInstructions = true,
                        IgnoreWhitespace = true
                    };

                    using var stringReader = new StringReader(rawContent);
                    using var reader = XmlReader.Create(stringReader, xmlSettings);

                    var sb = new StringBuilder();
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.SignificantWhitespace)
                        {
                            sb.Append(reader.Value);
                            sb.Append(' ');
                        }
                    }

                    string result = sb.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        return WebUtility.HtmlDecode(result);
                    }
                }
                catch
                {
                    // If XML parsing fails (malformed XML), fall back to regex-based extraction
                }

                try
                {
                    string text = rawContent;
                    text = LineBreakRegex.Replace(text, "\n");
                    text = BlockEndRegex.Replace(text, "\n");
                    text = TagRegex.Replace(text, " ");
                    return WebUtility.HtmlDecode(text).Trim();
                }
                catch
                {
                    return rawContent;
                }
            }

            return rawContent.Trim();
        }

        public static string NormalizeWhitespace(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return MultiSpaceRegex.Replace(input.Trim(), " ");
        }

        public static bool Matches(string title, string contentText, string searchQuery)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return true;
            }

            string normalizedQuery = NormalizeWhitespace(searchQuery);
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                return true;
            }

            string safeTitle = title ?? string.Empty;
            string safeContent = contentText ?? string.Empty;

            // Direct substring match first
            if (safeTitle.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                safeContent.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Multi-term matching: all words in search query must match either title or content
            var terms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length <= 1)
            {
                return false;
            }

            foreach (var term in terms)
            {
                bool termMatched = safeTitle.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   safeContent.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!termMatched)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
