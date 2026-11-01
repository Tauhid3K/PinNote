using System;
using System.IO;
using System.Linq;
using PinNote.Models;
using PinNote.Services;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    public class ReminderTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storage;
        private readonly MainViewModel _vm;

        public ReminderTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_reminder_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _storage = new StorageService(_testDir);
            _vm = new MainViewModel(customStorageService: _storage);
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
        public void NoteViewModel_DueDatesAndReminders_SetProperly()
        {
            _vm.CreateNewNote(openWindow: false);
            var note = _vm.Notes.Last();

            var pastDate = DateTime.UtcNow.AddMinutes(-5);
            note.DueDate = pastDate;
            note.ReminderDateTime = pastDate;

            Assert.True(note.HasDueDate);
            Assert.True(note.IsOverdue);
            Assert.True(note.HasReminder);
            Assert.False(note.IsReminderDismissed);

            note.DismissReminderCommand.Execute(null);
            Assert.True(note.IsReminderDismissed);
        }

        [Fact]
        public void ReminderService_TriggersDueReminders_AndAutoDismisses()
        {
            _vm.CreateNewNote(openWindow: false);
            var note = _vm.Notes.Last();
            note.Title = "Urgent Assignment";
            note.ReminderDateTime = DateTime.UtcNow.AddSeconds(-1);
            note.IsReminderDismissed = false;

            bool triggered = false;
            _vm.Reminders.ReminderTriggered += (s, triggeredNote) =>
            {
                if (triggeredNote.Id == note.Id)
                {
                    triggered = true;
                }
            };

            _vm.Reminders.CheckReminders();

            Assert.True(triggered);
            Assert.True(note.IsReminderDismissed);
        }

        [Fact]
        public void ReminderService_Snooze_UpdatesReminderTimeAndResetsDismissed()
        {
            _vm.CreateNewNote(openWindow: false);
            var note = _vm.Notes.Last();
            note.ReminderDateTime = DateTime.UtcNow.AddSeconds(-10);
            note.IsReminderDismissed = true;

            _vm.Reminders.SnoozeReminder(note, TimeSpan.FromMinutes(15));

            Assert.False(note.IsReminderDismissed);
            Assert.True(note.ReminderDateTime > DateTime.UtcNow);
        }

        [Fact]
        public void NoteViewModel_ReminderPresetsAndClear_WorkAsExpected()
        {
            _vm.CreateNewNote(openWindow: false);
            var note = _vm.Notes.Last();

            note.SetReminder1HourCommand.Execute(null);
            Assert.True(note.HasReminder);
            Assert.True(note.ReminderDateTime > DateTime.Now);

            note.ClearReminderCommand.Execute(null);
            Assert.False(note.HasReminder);
            Assert.Null(note.ReminderDateTime);
            Assert.Null(note.DueDate);
        }
    }
}
