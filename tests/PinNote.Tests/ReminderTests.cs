using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            NotificationService.Provider = null;
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

            // Presets must produce future UTC timestamps and expected local-time representations
            note.SetReminder10MinutesCommand.Execute(null);
            Assert.True(note.HasReminder);
            Assert.True(note.ReminderDateTime > DateTime.UtcNow);
            Assert.Equal(DateTimeKind.Utc, note.ReminderDateTime!.Value.Kind);

            note.SetReminder1HourCommand.Execute(null);
            Assert.True(note.HasReminder);
            Assert.True(note.ReminderDateTime > DateTime.UtcNow);
            Assert.True(note.ReminderDateTime.Value.ToLocalTime() > DateTime.Now);

            note.SetReminderTodayEveningCommand.Execute(null);
            Assert.True(note.ReminderDateTime > DateTime.UtcNow);

            note.SetReminderTomorrowMorningCommand.Execute(null);
            Assert.True(note.ReminderDateTime > DateTime.UtcNow);
            Assert.True(note.ReminderDateTime.Value.ToLocalTime().Date > DateTime.Today);

            note.SetReminderNextWeekCommand.Execute(null);
            Assert.True(note.ReminderDateTime > DateTime.UtcNow);

            // Clearing reminder
            note.ClearReminderCommand.Execute(null);
            Assert.False(note.HasReminder);
            Assert.Null(note.ReminderDateTime);
        }

        [Fact]
        public void NoteViewModel_CustomDateTime_NormalizesToUtc()
        {
            _vm.CreateNewNote(openWindow: false);
            var note = _vm.Notes.Last();

            // Local kind
            DateTime localTarget = DateTime.Now.AddDays(2);
            note.ReminderDateTime = localTarget;
            Assert.Equal(DateTimeKind.Utc, note.ReminderDateTime!.Value.Kind);
            Assert.Equal(localTarget.ToUniversalTime(), note.ReminderDateTime.Value);

            // Unspecified kind (e.g. from standard date pickers)
            DateTime unspecified = new DateTime(2027, 5, 10, 14, 30, 0, DateTimeKind.Unspecified);
            note.ReminderDateTime = unspecified;
            Assert.Equal(DateTimeKind.Utc, note.ReminderDateTime!.Value.Kind);
            Assert.Equal(DateTime.SpecifyKind(unspecified, DateTimeKind.Local).ToUniversalTime(), note.ReminderDateTime.Value);
        }

        [Fact]
        public void ReminderService_OverdueReminders_TriggerOnStartup()
        {
            // Acceptance criteria: Test overdue reminders after restarting the application
            var overdueNote = new NoteModel
            {
                Id = Guid.NewGuid(),
                Title = "Missed Deadline Note",
                ReminderDateTime = DateTime.UtcNow.AddMinutes(-30),
                IsReminderDismissed = false
            };
            _storage.SaveNotes(new System.Collections.Generic.List<NoteModel> { overdueNote });

            // Simulating app restart
            var restartedVm = new MainViewModel(customStorageService: _storage);
            bool missedReminderTriggered = false;
            restartedVm.Reminders.ReminderTriggered += (_, n) =>
            {
                if (n.Id == overdueNote.Id) missedReminderTriggered = true;
            };

            // Start triggers startup check for missed reminders
            restartedVm.Reminders.Start();

            Assert.True(missedReminderTriggered);
            var loaded = restartedVm.Notes.First(n => n.Id == overdueNote.Id);
            Assert.True(loaded.IsReminderDismissed);
            restartedVm.Reminders.Stop();
        }

        [Fact]
        public void ReminderService_NotificationFailure_DoesNotDismissReminder()
        {
            // Acceptance criteria: Do not dismiss a reminder unless its notification was successfully delivered
            var fakeNotifier = new MockNotificationService { ShouldSucceed = false };
            NotificationService.Provider = fakeNotifier;

            _vm.CreateNewNote(openWindow: false);
            var note = _vm.Notes.Last();
            note.ReminderDateTime = DateTime.UtcNow.AddSeconds(-5);
            note.IsReminderDismissed = false;

            // Delivery fails
            _vm.Reminders.CheckReminders();
            Assert.False(note.IsReminderDismissed);
            Assert.Equal(1, fakeNotifier.CallCount);

            // Now delivery succeeds
            fakeNotifier.ShouldSucceed = true;
            _vm.Reminders.CheckReminders();
            Assert.True(note.IsReminderDismissed);
            Assert.Equal(2, fakeNotifier.CallCount);
        }

        [Fact]
        public void ReminderService_ThreadSafety_DoesNotThrowCrossThreadExceptions()
        {
            // Acceptance criteria: Verify no cross-thread exceptions occur
            _vm.CreateNewNote(openWindow: false);
            var note = _vm.Notes.Last();
            note.ReminderDateTime = DateTime.UtcNow.AddSeconds(-2);
            note.IsReminderDismissed = false;

            // Run check on a background thread pool thread
            var exception = Record.Exception(() =>
            {
                Task.Run(() => _vm.Reminders.CheckReminders()).GetAwaiter().GetResult();
            });

            Assert.Null(exception);
            Assert.True(note.IsReminderDismissed);
        }

        private class MockNotificationService : INotificationService
        {
            public bool ShouldSucceed { get; set; } = true;
            public int CallCount { get; private set; }

            public bool ShowNotification(string title, string message)
            {
                CallCount++;
                return ShouldSucceed;
            }
        }
    }
}
