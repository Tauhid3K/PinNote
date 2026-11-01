using System.Collections.Generic;
using PinNote.Models;
using PinNote.Services;
using Xunit;

namespace PinNote.Tests
{
    public class SearchServiceTests
    {
        [Theory]
        [InlineData("Meeting", "Important project meeting tomorrow", "meeting", true)]
        [InlineData("Meeting", "Important project meeting tomorrow", "PROJECT", true)]
        [InlineData("Meeting", "Important project meeting tomorrow", "TOMORROW", true)]
        [InlineData("Shopping", "Buy apples and milk", "oranges", false)]
        public void Matches_BasicCaseInsensitive_ReturnsExpected(string title, string content, string query, bool expected)
        {
            bool result = SearchService.Matches(title, content, query);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Matches_WithIrregularWhitespace_MatchesCorrectly()
        {
            string title = "Project   Alpha";
            string content = "Line 1\n\n\tLine   2";

            Assert.True(SearchService.Matches(title, content, "project alpha"));
            Assert.True(SearchService.Matches(title, content, "Line 1"));
            Assert.True(SearchService.Matches(title, content, "Line 2"));
            Assert.True(SearchService.Matches(title, content, "  project   "));
        }

        [Fact]
        public void Matches_MultiTermSearch_MatchesWordsAcrossTitleAndContent()
        {
            string title = "Weekly Grocery List";
            string content = "Buy oat milk and whole wheat bread";

            // Both "grocery" and "bread" appear across title + content
            Assert.True(SearchService.Matches(title, content, "grocery bread"));
            // "milk" and "weekly"
            Assert.True(SearchService.Matches(title, content, "weekly milk"));
            // One term missing
            Assert.False(SearchService.Matches(title, content, "weekly cheese"));
        }

        [Fact]
        public void ExtractSearchableText_FromFlowDocumentXaml_ExtractsCleanText()
        {
            string xaml = "<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
                          "<Paragraph>First paragraph text.</Paragraph>" +
                          "<Paragraph><Run FontWeight=\"Bold\">Bold run</Run><LineBreak/>Second line.</Paragraph>" +
                          "</FlowDocument>";

            string text = SearchService.ExtractSearchableText(xaml, NoteType.Text, null);

            Assert.Contains("First paragraph text.", text);
            Assert.Contains("Bold run", text);
            Assert.Contains("Second line.", text);
            Assert.DoesNotContain("<FlowDocument", text);
            Assert.DoesNotContain("<Paragraph", text);
        }

        [Fact]
        public void ExtractSearchableText_FromMalformedXaml_DoesNotThrowAndReturnsContent()
        {
            string malformed = "<FlowDocument><Paragraph>Unclosed tag and <invalid & entity";

            string text = SearchService.ExtractSearchableText(malformed, NoteType.Text, null);

            Assert.NotNull(text);
            Assert.Contains("Unclosed tag", text);
        }

        [Fact]
        public void ExtractSearchableText_FromChecklistItems_IncludesItemsAndState()
        {
            var items = new List<ChecklistItemModel>
            {
                new() { Text = "Buy groceries", IsChecked = false },
                new() { Text = "Pay electric bill", IsChecked = true }
            };

            string text = SearchService.ExtractSearchableText(null, NoteType.Checklist, items);

            Assert.Contains("[ ] Buy groceries", text);
            Assert.Contains("[x] Pay electric bill", text);
        }
    }
}
