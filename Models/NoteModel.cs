using System;
using System.Collections.Generic;
using System.Linq;

namespace PinNote.Models
{
    public class NoteModel
    {
        public int SchemaVersion { get; set; } = 1;
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "New Note";
        public string Content { get; set; } = "";
        public double X { get; set; } = 100;
        public double Y { get; set; } = 100;
        public double Width { get; set; } = 250;
        public double Height { get; set; } = 250;
        public double Opacity { get; set; } = 1.0;
        public double TextOpacity { get; set; } = 1.0;
        public string TitleBarColor { get; set; } = "#FFD1EAF7";
        public string BodyColor { get; set; } = "#FFFFFFFF";
        public bool IsPinned { get; set; } = false;
        public double TitleFontSize { get; set; } = 18;
        public double BodyFontSize { get; set; } = 20;
        public bool IsCrystalClear { get; set; } = false;
        public string? CustomTextColor { get; set; } = null;
        public NoteType NoteType { get; set; } = NoteType.Text;
        public List<ChecklistItemModel> ChecklistItems { get; set; } = new();
        public bool IsVisible { get; set; } = true;

        /// <summary>UTC timestamp when this note was first created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp of the last edit to title or content. Updated on every save.</summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        // Tags & Categories (Phase 2 & 3)
        public List<string> Tags { get; set; } = new();
        public string? Category { get; set; } = null;

        // Workspace (Phase 2 & 3)
        public Guid? WorkspaceId { get; set; } = null;
        public string WorkspaceName { get; set; } = "Default";

        // Reminders & Due Dates (Phase 2 & 4)
        public DateTime? DueDate { get; set; } = null;
        public DateTime? ReminderDateTime { get; set; } = null;
        public bool IsReminderDismissed { get; set; } = false;

        // Markdown preview switch (Phase 2 & 4)
        public bool IsMarkdownPreview { get; set; } = false;

        // Templates metadata (Phase 2 & 4)
        public bool IsTemplate { get; set; } = false;
        public string? TemplateName { get; set; } = null;

        public NoteModel Clone()
        {
            return new NoteModel
            {
                SchemaVersion = SchemaVersion,
                Id = Id,
                Title = Title,
                Content = Content,
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
                Opacity = Opacity,
                TextOpacity = TextOpacity,
                TitleBarColor = TitleBarColor,
                BodyColor = BodyColor,
                IsPinned = IsPinned,
                TitleFontSize = TitleFontSize,
                BodyFontSize = BodyFontSize,
                IsCrystalClear = IsCrystalClear,
                CustomTextColor = CustomTextColor,
                NoteType = NoteType,
                ChecklistItems = ChecklistItems?.Select(item => item.Clone()).ToList() ?? new List<ChecklistItemModel>(),
                IsVisible = IsVisible,
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                Tags = Tags?.ToList() ?? new List<string>(),
                Category = Category,
                WorkspaceId = WorkspaceId,
                WorkspaceName = WorkspaceName,
                DueDate = DueDate,
                ReminderDateTime = ReminderDateTime,
                IsReminderDismissed = IsReminderDismissed,
                IsMarkdownPreview = IsMarkdownPreview,
                IsTemplate = IsTemplate,
                TemplateName = TemplateName
            };
        }
    }
}
