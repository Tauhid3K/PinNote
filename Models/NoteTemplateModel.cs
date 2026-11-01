using System;
using System.Collections.Generic;
using System.Linq;

namespace PinNote.Models
{
    public class NoteTemplateModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Template";
        public string Description { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public NoteType NoteType { get; set; } = NoteType.Text;
        public List<ChecklistItemModel> ChecklistItems { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public string? Category { get; set; } = null;
        public bool IsBuiltIn { get; set; } = false;

        public NoteTemplateModel Clone()
        {
            return new NoteTemplateModel
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Content = Content,
                NoteType = NoteType,
                ChecklistItems = ChecklistItems?.Select(i => i.Clone()).ToList() ?? new List<ChecklistItemModel>(),
                Tags = Tags?.ToList() ?? new List<string>(),
                Category = Category,
                IsBuiltIn = IsBuiltIn
            };
        }

        public NoteModel CreateNote()
        {
            return new NoteModel
            {
                Id = Guid.NewGuid(),
                Title = Name,
                Content = Content,
                NoteType = NoteType,
                ChecklistItems = ChecklistItems?.Select(i => i.Clone()).ToList() ?? new List<ChecklistItemModel>(),
                Tags = Tags?.ToList() ?? new List<string>(),
                Category = Category,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                IsVisible = true
            };
        }
    }
}
