using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PinNote.Models;

namespace PinNote.Services
{
    public static class MarkdownService
    {
        private static readonly Regex FencedCodeRegex = new(@"```([a-zA-Z0-9_-]*)\r?\n([\s\S]*?)```", RegexOptions.Compiled);
        private static readonly Regex InlineCodeRegex = new(@"`([^`\r\n]+)`", RegexOptions.Compiled);
        private static readonly Regex BoldRegex = new(@"(\*\*|__)(.*?)\1", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new(@"(?<!\*|_)([*_])([^*_\r\n]+?)\1(?!\*|_)", RegexOptions.Compiled);
        private static readonly Regex StrikeRegex = new(@"~~(.*?)~~", RegexOptions.Compiled);
        private static readonly Regex ImageRegex = new(@"!\[(.*?)\]\((.*?)\)", RegexOptions.Compiled);
        private static readonly Regex LinkRegex = new(@"\[(.*?)\]\((.*?)\)", RegexOptions.Compiled);

        public static string RenderToHtml(string? markdown, string bodyBg = "#FFFFFF", string textColor = "#1F2937")
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return "<html><body style='font-family:Segoe UI, sans-serif; font-size:14px; color:#9CA3AF;'><em>Empty note</em></body></html>";
            }

            // Step 1: Escape all raw HTML for security (Safe rendering without arbitrary HTML execution)
            string encoded = WebUtility.HtmlEncode(markdown);

            // Step 2: Handle fenced code blocks first to protect code content
            var codeBlocks = new List<string>();
            encoded = FencedCodeRegex.Replace(encoded, match =>
            {
                string lang = match.Groups[1].Value;
                string code = match.Groups[2].Value;
                int index = codeBlocks.Count;
                codeBlocks.Add($"<pre style='background:#F3F4F6; color:#1F2937; padding:8px 12px; border-radius:6px; overflow-x:auto;'><code>{code}</code></pre>");
                return $"@@@CODEBLOCK{index}@@@";
            });

            // Step 3: Handle inline code
            encoded = InlineCodeRegex.Replace(encoded, "<code style='background:#F3F4F6; padding:2px 4px; border-radius:4px; font-family:monospace;'>$1</code>");

            // Step 4: Handle headings, blockquotes, lists line-by-line
            var lines = encoded.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            bool inUl = false;
            bool inOl = false;

            foreach (var rawLine in lines)
            {
                string line = rawLine;

                // Check for lists
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("- [ ] ") || trimmed.StartsWith("- [x] ") || trimmed.StartsWith("- [X] "))
                {
                    if (inOl) { sb.AppendLine("</ol>"); inOl = false; }
                    if (!inUl) { sb.AppendLine("<ul style='list-style-type:none; padding-left:4px; margin:4px 0;'>"); inUl = true; }

                    bool isChecked = !trimmed.StartsWith("- [ ] ");
                    string checkText = trimmed.Substring(6);
                    string checkHtml = isChecked
                        ? "<input type='checkbox' checked disabled style='margin-right:6px;' />"
                        : "<input type='checkbox' disabled style='margin-right:6px;' />";

                    string style = isChecked ? "text-decoration:line-through; opacity:0.7;" : "";
                    sb.AppendLine($"<li style='margin:3px 0; {style}'>{checkHtml}{ProcessInlines(checkText)}</li>");
                    continue;
                }

                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    if (inOl) { sb.AppendLine("</ol>"); inOl = false; }
                    if (!inUl) { sb.AppendLine("<ul style='padding-left:20px; margin:4px 0;'>"); inUl = true; }
                    sb.AppendLine($"<li style='margin:3px 0;'>{ProcessInlines(trimmed.Substring(2))}</li>");
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\d+\.\s"))
                {
                    if (inUl) { sb.AppendLine("</ul>"); inUl = false; }
                    if (!inOl) { sb.AppendLine("<ol style='padding-left:20px; margin:4px 0;'>"); inOl = true; }
                    string itemText = Regex.Replace(trimmed, @"^\d+\.\s", "");
                    sb.AppendLine($"<li style='margin:3px 0;'>{ProcessInlines(itemText)}</li>");
                    continue;
                }

                // If not in a list line, close lists
                if (inUl) { sb.AppendLine("</ul>"); inUl = false; }
                if (inOl) { sb.AppendLine("</ol>"); inOl = false; }

                // Headings
                if (trimmed.StartsWith("### "))
                {
                    sb.AppendLine($"<h3>{ProcessInlines(trimmed.Substring(4))}</h3>");
                    continue;
                }
                if (trimmed.StartsWith("## "))
                {
                    sb.AppendLine($"<h2>{ProcessInlines(trimmed.Substring(3))}</h2>");
                    continue;
                }
                if (trimmed.StartsWith("# "))
                {
                    sb.AppendLine($"<h1>{ProcessInlines(trimmed.Substring(2))}</h1>");
                    continue;
                }

                // Blockquotes
                if (trimmed.StartsWith("&gt; ") || trimmed.StartsWith("> "))
                {
                    string quoteText = trimmed.StartsWith("&gt; ") ? trimmed.Substring(5) : trimmed.Substring(2);
                    sb.AppendLine($"<blockquote>{ProcessInlines(quoteText)}</blockquote>");
                    continue;
                }

                // Empty line
                if (string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine("<div style='height:8px;'></div>");
                    continue;
                }

                // Code block placeholder line
                if (trimmed.StartsWith("@@@CODEBLOCK") && trimmed.EndsWith("@@@"))
                {
                    sb.AppendLine(trimmed);
                    continue;
                }

                // Normal paragraph / line
                sb.AppendLine($"<p style='margin:2px 0;'>{ProcessInlines(line)}</p>");
            }

            if (inUl) sb.AppendLine("</ul>");
            if (inOl) sb.AppendLine("</ol>");

            string rendered = sb.ToString();

            // Re-insert code blocks
            for (int i = 0; i < codeBlocks.Count; i++)
            {
                rendered = rendered.Replace($"@@@CODEBLOCK{i}@@@", codeBlocks[i]);
            }

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<style>
body {{
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    font-size: 14px;
    line-height: 1.5;
    background-color: {bodyBg};
    color: {textColor};
    margin: 4px;
    padding: 0;
    word-break: break-word;
}}
h1 {{ margin: 12px 0 6px 0; font-size: 1.5em; font-weight: bold; }}
h2 {{ margin: 10px 0 6px 0; font-size: 1.3em; font-weight: bold; }}
h3 {{ margin: 8px 0 4px 0; font-size: 1.15em; font-weight: 600; }}
blockquote {{ border-left: 3px solid #D1D5DB; margin: 4px 0; padding-left: 10px; color: #4B5563; font-style: italic; }}
a {{ color: #2563EB; text-decoration: underline; }}
img {{ max-width: 100%; border-radius: 4px; }}
</style>
</head>
<body>
{rendered}
</body>
</html>";
        }

        private static string ProcessInlines(string text)
        {
            // Bold
            text = BoldRegex.Replace(text, "<strong>$2</strong>");
            // Strikethrough
            text = StrikeRegex.Replace(text, "<del>$1</del>");
            // Italic
            text = ItalicRegex.Replace(text, "<em>$2</em>");

            // Images ![alt](url)
            text = ImageRegex.Replace(text, match =>
            {
                string alt = match.Groups[1].Value;
                string url = match.Groups[2].Value;
                if (IsSafeUrl(url))
                {
                    return $"<img src='{url}' alt='{alt}' />";
                }
                return $"[Image: {alt}]";
            });

            // Links [text](url)
            text = LinkRegex.Replace(text, match =>
            {
                string linkText = match.Groups[1].Value;
                string url = match.Groups[2].Value;
                if (IsSafeUrl(url))
                {
                    return $"<a href='{url}' target='_blank' rel='noopener noreferrer'>{linkText}</a>";
                }
                return linkText;
            });

            return text;
        }

        private static bool IsSafeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string trimmed = url.Trim();
            return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<bool> ExportToMarkdownFileAsync(NoteModel note, string destinationPath)
        {
            try
            {
                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(note.Title))
                {
                    sb.AppendLine($"# {note.Title}");
                    sb.AppendLine();
                }

                if (note.Tags != null && note.Tags.Count > 0)
                {
                    sb.AppendLine($"Tags: {string.Join(", ", note.Tags)}");
                    sb.AppendLine();
                }

                if (note.NoteType == NoteType.Checklist && note.ChecklistItems != null)
                {
                    foreach (var item in note.ChecklistItems)
                    {
                        sb.AppendLine(item.IsChecked ? $"- [x] {item.Text}" : $"- [ ] {item.Text}");
                    }
                }
                else
                {
                    sb.AppendLine(note.Content);
                }

                string dir = Path.GetDirectoryName(destinationPath) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllTextAsync(destinationPath, sb.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExportToMarkdownFileAsync error: {ex}");
                return false;
            }
        }

        public static async Task<NoteModel?> ImportFromMarkdownFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                string text = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                string title = Path.GetFileNameWithoutExtension(filePath);
                var bodyLines = new List<string>();
                bool foundFirstHeader = false;

                foreach (var line in lines)
                {
                    if (!foundFirstHeader && line.StartsWith("# "))
                    {
                        title = line.Substring(2).Trim();
                        foundFirstHeader = true;
                        continue;
                    }
                    bodyLines.Add(line);
                }

                string body = string.Join("\n", bodyLines).Trim();

                return new NoteModel
                {
                    Id = Guid.NewGuid(),
                    Title = string.IsNullOrWhiteSpace(title) ? "Imported Markdown" : title,
                    Content = body,
                    NoteType = NoteType.Markdown,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    IsVisible = true
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImportFromMarkdownFileAsync error: {ex}");
                return null;
            }
        }

        public static List<MarkdownBlock> ParseBlocks(string? markdown)
        {
            var blocks = new List<MarkdownBlock>();
            if (string.IsNullOrWhiteSpace(markdown)) return blocks;

            var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool inCode = false;
            var codeSb = new StringBuilder();
            string codeLang = string.Empty;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (trimmed.StartsWith("```"))
                {
                    if (inCode)
                    {
                        blocks.Add(new MarkdownBlock
                        {
                            Type = MarkdownBlockType.Code,
                            Text = codeSb.ToString().TrimEnd(),
                            Language = codeLang
                        });
                        codeSb.Clear();
                        inCode = false;
                    }
                    else
                    {
                        inCode = true;
                        codeLang = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : string.Empty;
                    }
                    continue;
                }

                if (inCode)
                {
                    codeSb.AppendLine(line);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                if (trimmed.StartsWith("### "))
                {
                    blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Heading3, Text = CleanInlineText(trimmed.Substring(4).Trim()) });
                }
                else if (trimmed.StartsWith("## "))
                {
                    blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Heading2, Text = CleanInlineText(trimmed.Substring(3).Trim()) });
                }
                else if (trimmed.StartsWith("# "))
                {
                    blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Heading1, Text = CleanInlineText(trimmed.Substring(2).Trim()) });
                }
                else if (trimmed.StartsWith("> "))
                {
                    blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Quote, Text = CleanInlineText(trimmed.Substring(2).Trim()) });
                }
                else if (trimmed.StartsWith("- [ ] ") || trimmed.StartsWith("- [x] ") || trimmed.StartsWith("- [X] "))
                {
                    bool isChecked = !trimmed.StartsWith("- [ ] ");
                    blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Checklist, IsChecked = isChecked, Text = CleanInlineText(trimmed.Substring(6).Trim()) });
                }
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Bullet, Text = CleanInlineText(trimmed.Substring(2).Trim()) });
                }
                else
                {
                    blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Paragraph, Text = CleanInlineText(trimmed) });
                }
            }

            if (inCode && codeSb.Length > 0)
            {
                blocks.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.Code,
                    Text = codeSb.ToString().TrimEnd(),
                    Language = codeLang
                });
            }

            return blocks;
        }

        private static string CleanInlineText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            // Clean markdown syntax for standard block display
            string s = raw;
            s = BoldRegex.Replace(s, "$2");
            s = ItalicRegex.Replace(s, "$2");
            s = StrikeRegex.Replace(s, "$1");
            s = InlineCodeRegex.Replace(s, "$1");
            return s;
        }
    }

    public enum MarkdownBlockType
    {
        Heading1,
        Heading2,
        Heading3,
        Paragraph,
        Bullet,
        Checklist,
        Quote,
        Code
    }

    public class MarkdownBlock
    {
        public MarkdownBlockType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public string Language { get; set; } = string.Empty;

        public bool IsHeading1 => Type == MarkdownBlockType.Heading1;
        public bool IsHeading2 => Type == MarkdownBlockType.Heading2;
        public bool IsHeading3 => Type == MarkdownBlockType.Heading3;
        public bool IsParagraph => Type == MarkdownBlockType.Paragraph;
        public bool IsBullet => Type == MarkdownBlockType.Bullet;
        public bool IsChecklist => Type == MarkdownBlockType.Checklist;
        public bool IsQuote => Type == MarkdownBlockType.Quote;
        public bool IsCode => Type == MarkdownBlockType.Code;
    }
}
