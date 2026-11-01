using System;

namespace PinNote.Models
{
    public class WorkspaceModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Default";
        public bool IsDefault { get; set; } = false;
        public int Order { get; set; } = 0;

        public WorkspaceModel Clone()
        {
            return new WorkspaceModel
            {
                Id = Id,
                Name = Name,
                IsDefault = IsDefault,
                Order = Order
            };
        }
    }
}
