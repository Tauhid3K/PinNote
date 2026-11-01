using System;

namespace PinNote.Models
{
    public class ChecklistItemModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Text { get; set; } = string.Empty;
        public bool IsChecked { get; set; } = false;
        public int Order { get; set; } = 0;

        public ChecklistItemModel Clone()
        {
            return new ChecklistItemModel
            {
                Id = Id,
                Text = Text,
                IsChecked = IsChecked,
                Order = Order
            };
        }
    }
}
