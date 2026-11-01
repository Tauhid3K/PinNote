using System;
using System.IO;
using System.Threading.Tasks;
using PinNote.Models;
using PinNote.Services;
using Xunit;

namespace PinNote.Tests
{
    public class MarkdownTests : IDisposable
    {
        private readonly string _testDir;

        public MarkdownTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_md_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void MarkdownService_RendersHeadingsListsAndFormatting()
        {
            string md = @"# Heading 1
## Heading 2
**Bold Text** and *Italic Text* and ~~Strike~~
- Item 1
- Item 2
- [ ] Task incomplete
- [x] Task complete
```csharp
var x = 10;
```
> Important quote";

            string html = MarkdownService.RenderToHtml(md);

            Assert.Contains("<h1>Heading 1</h1>", html);
            Assert.Contains("<h2>Heading 2</h2>", html);
            Assert.Contains("<strong>Bold Text</strong>", html);
            Assert.Contains("<em>Italic Text</em>", html);
            Assert.Contains("<del>Strike</del>", html);
            Assert.Contains("<input type='checkbox' disabled", html);
            Assert.Contains("<input type='checkbox' checked disabled", html);
            Assert.Contains("<pre", html);
            Assert.Contains("<blockquote>", html);
        }

        [Fact]
        public void MarkdownService_PreventsArbitraryHtmlExecution()
        {
            string maliciousMd = "<script>alert('pwned')</script><iframe src='javascript:void(0)'></iframe>";
            string html = MarkdownService.RenderToHtml(maliciousMd);

            // Raw dangerous tags must be HTML-encoded
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.DoesNotContain("<iframe", html);
            Assert.Contains("&lt;iframe", html);
        }

        [Fact]
        public async Task MarkdownService_ExportAndImportRoundtrip()
        {
            var note = new NoteModel
            {
                Title = "Math Lecture",
                Content = "## Calculus I\nDerivatives and limits",
                NoteType = NoteType.Markdown,
                Tags = new System.Collections.Generic.List<string> { "Math" }
            };

            string filePath = Path.Combine(_testDir, "lecture.md");
            bool exported = await MarkdownService.ExportToMarkdownFileAsync(note, filePath);

            Assert.True(exported);
            Assert.True(File.Exists(filePath));

            var imported = await MarkdownService.ImportFromMarkdownFileAsync(filePath);

            Assert.NotNull(imported);
            Assert.Equal("Math Lecture", imported!.Title);
            Assert.Contains("Calculus I", imported.Content);
            Assert.Equal(NoteType.Markdown, imported.NoteType);
        }

        [Fact]
        public void MarkdownService_ParseBlocks_CorrectlyIdentifiesBlockTypes()
        {
            string md = @"# Heading 1
## Heading 2
### Heading 3
This is a standard paragraph with **bold** text.
- Bullet item 1
- [ ] Incomplete task
- [x] Complete task
> Blockquote text
```csharp
int a = 5;
```";

            var blocks = MarkdownService.ParseBlocks(md);

            Assert.Equal(9, blocks.Count);
            Assert.Equal(MarkdownBlockType.Heading1, blocks[0].Type);
            Assert.Equal("Heading 1", blocks[0].Text);

            Assert.Equal(MarkdownBlockType.Heading2, blocks[1].Type);
            Assert.Equal("Heading 2", blocks[1].Text);

            Assert.Equal(MarkdownBlockType.Heading3, blocks[2].Type);
            Assert.Equal("Heading 3", blocks[2].Text);

            Assert.Equal(MarkdownBlockType.Paragraph, blocks[3].Type);
            Assert.Equal("This is a standard paragraph with bold text.", blocks[3].Text);

            Assert.Equal(MarkdownBlockType.Bullet, blocks[4].Type);
            Assert.Equal("Bullet item 1", blocks[4].Text);

            Assert.Equal(MarkdownBlockType.Checklist, blocks[5].Type);
            Assert.False(blocks[5].IsChecked);
            Assert.Equal("Incomplete task", blocks[5].Text);

            Assert.Equal(MarkdownBlockType.Checklist, blocks[6].Type);
            Assert.True(blocks[6].IsChecked);
            Assert.Equal("Complete task", blocks[6].Text);

            Assert.Equal(MarkdownBlockType.Quote, blocks[7].Type);
            Assert.Equal("Blockquote text", blocks[7].Text);

            Assert.Equal(MarkdownBlockType.Code, blocks[8].Type);
            Assert.Equal("csharp", blocks[8].Language);
            Assert.Contains("int a = 5;", blocks[8].Text);
        }
    }
}
