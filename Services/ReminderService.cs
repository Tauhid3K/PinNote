using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using PinNote.ViewModels;

namespace PinNote.Services
{
    public class ReminderService : IDisposable
    {
        private readonly MainViewModel _mainViewModel;
        private readonly Timer _checkTimer;
        private readonly HashSet<string> _notifiedKeys = new();
        private bool _isDisposed;

        public event EventHandler<NoteViewModel>? ReminderTriggered;

        public ReminderService(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _checkTimer = new Timer(15000) { AutoReset = true }; // Check every 15s
            _checkTimer.Elapsed += (_, _) => CheckReminders();
        }

        public void Start()
        {
            _checkTimer.Start();
            // Perform an initial check for missed reminders on startup
            CheckReminders(isStartupCheck: true);
        }

        public void Stop()
        {
            _checkTimer.Stop();
        }

        public void CheckReminders(bool isStartupCheck = false)
        {
            try
            {
                var now = DateTime.UtcNow;
                var notes = _mainViewModel.Notes.ToList();

                foreach (var note in notes)
                {
                    if (note.ReminderDateTime.HasValue && !note.IsReminderDismissed)
                    {
                        var reminderTime = note.ReminderDateTime.Value;
                        string key = $"{note.Id}_{reminderTime.Ticks}";

                        if (reminderTime <= now && !_notifiedKeys.Contains(key))
                        {
                            _notifiedKeys.Add(key);

                            string title = isStartupCheck
                                ? $"Missed Reminder: {note.Title}"
                                : $"Reminder: {note.Title}";

                            string preview = !string.IsNullOrWhiteSpace(note.PlainTextPreview)
                                ? note.PlainTextPreview
                                : "Due note reminder";

                            NotificationService.ShowNotification(title, preview);

                            ReminderTriggered?.Invoke(this, note);

                            // Auto-mark dismissed once delivered
                            note.IsReminderDismissed = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReminderService error: {ex}");
            }
        }

        public void SnoozeReminder(NoteViewModel note, TimeSpan duration)
        {
            if (note == null) return;
            note.ReminderDateTime = DateTime.UtcNow.Add(duration);
            note.IsReminderDismissed = false;
        }

        public void DismissReminder(NoteViewModel note)
        {
            if (note == null) return;
            note.IsReminderDismissed = true;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _checkTimer.Stop();
                _checkTimer.Dispose();
            }
        }
    }
}
