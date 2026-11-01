# PinNote Project Instructions

## Project Overview
PinNote is a cross-platform desktop sticky note and productivity application built with .NET 8, WPF for Windows, and Avalonia UI for Linux.

## Development Workflow

## [Cross-Platform Architecture & Productivity Improvements] - November 1, 2026

### Changes
- **Background Storage & Concurrency (P0)**: Asynchronous file operations off the UI thread with monotonic timestamp protection, pending save flushing on shutdown, and safe debouncing.
- **Data Model & Migration Foundation (P1)**: Versioned `NoteModel` (schema v2) with automatic backup creation (`notes_migration_backup_v1_*.json`) before upgrading legacy notes.
- **Tags & Categories (P2)**: Multi-tag assignment, default categories (`Study`, `Personal`, `Work`, `Projects`), and combined search/filter logic.
- **Workspace Groups (P3)**: Multiple workspaces (`Default`, `University`, `Personal`, `Development`, `Projects`) with safe deletion reassigning notes to `Default`.
- **Reminders & Due Dates (P4)**: Scheduling, missed reminder recovery on startup, notification dispatch, and quick preset intervals.
- **Markdown Editing & Preview (P5)**: Safe rendering (HTML-encoded), heading/list/quote/code formatting, and structured block parsing for native Avalonia and WPF views.
- **Reusable Templates (P6)**: Built-in student & productivity templates (`Lecture Notes`, `Exam Preparation`, `Daily Planner`, `Meeting Notes`, `Project Planning`, `Personal To-do List`) with custom template persistence.
- **Accessibility & Keyboard Navigation (P7)**: Added `AutomationProperties.Name` across controls, standard shortcuts (`Ctrl+N`, `Ctrl+P`, `Ctrl+T`, `Ctrl+M`, `Escape`), and checklist Enter/Backspace workflows.
- **Linux Wayland Shortcuts & Diagnostics (P8)**: Diagnostics report with desktop-specific setup instructions for GNOME, KDE Plasma, Sway, and Wayland fallback.

### Verification
- `dotnet build` succeeded with 0 warnings and 0 errors.
- `dotnet test tests/PinNote.Tests/PinNote.Tests.csproj` succeeded: 104 passed, 0 failed, 0 skipped.

- Follow MVVM strictly.
- Keep services decoupled and focused on single responsibilities.
- Use `Newtonsoft.Json` for data persistence.
