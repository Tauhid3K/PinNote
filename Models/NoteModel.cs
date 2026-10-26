using System;

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
                CustomTextColor = CustomTextColor
            };
        }
    }
}
