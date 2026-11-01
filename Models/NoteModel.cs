using System;
using System.Collections.Generic;
using System.Linq;

namespace PinNote.Models
{
    public class NoteModel
    {
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

        /// <summary>UTC timestamp when this note was first created. Defaults to DateTime.UtcNow on creation
        /// or when migrated from older versions that lacked timestamp fields.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp of the last edit to title or content. Updated by NoteViewModel on every save.</summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        public NoteModel Clone()
        {
            return new NoteModel
            {
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
                ModifiedAt = ModifiedAt
            };
        }
    }
}
