using System;
using System.Collections.Generic;

namespace PinNote.Models
{
    /// <summary>
    /// Documented, platform-independent JSON interchange format for PinNote.
    /// Used for backup, export, and import operations.
    /// </summary>
    public class NoteExportPackage
    {
        public int SchemaVersion { get; set; } = 1;
        public string Application { get; set; } = "PinNote";
        public string AppVersion { get; set; } = "1.0.3";
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
        public List<NoteModel> Notes { get; set; } = new();
    }
}
