# PinNote Project Instructions

## Project Overview
PinNote is a cross-platform desktop sticky note and productivity application built with .NET 8, WPF for Windows, and Avalonia UI for Linux.

## Development Workflow

## [Version 1.0.0 Unified Release & Synchronization] - November 19, 2026

### Changes
- **Standardized Version to 1.0.0**: Unified version numbering across the entire application, landing page, and build scripts to official initial production release `1.0.0`.
- **Synchronized Declarations**: Aligned `Version`, `AssemblyVersion`, and `FileVersion` across `PinNote.csproj`, `NoteExportPackage.cs`, `ImportExportService.cs`, `UpdateCheckService.cs`, `installer/PinNote.iss`, `scripts/build-installer.ps1`, `scripts/build-linux.sh`, `scripts/install-linux.sh`, `README.md`, `website/package.json`, `website/index.html`, and `website/app.js`.
- **Linux Packages & Distribution**: Standardized distribution targets to `PinNote-Setup-1.0.0.sh` and `PinNote-1.0.0-linux-x64.tar.gz`.

## [Version 1.1.0 Milestone Release & Semantic Versioning Bump] - November 15, 2026

### Changes
- **Semantic Versioning Minor Milestone (1.1.0)**: Upgraded version numbering from patch sequence `1.0.x` to minor milestone `1.1.0` following SemVer 2.0.0 rules to reflect backward-compatible feature additions (Workspaces, Templates, Reminders, Markdown mode, Update Notifications, and Import/Export safety guarantees).
- **Synchronized Version Declarations**: Aligned `Version`, `AssemblyVersion`, and `FileVersion` across `PinNote.csproj`, `NoteExportPackage.cs`, `ImportExportService.cs`, `UpdateCheckService.cs`, `installer/PinNote.iss`, `scripts/build-installer.ps1`, `scripts/build-linux.sh`, `scripts/install-linux.sh`, and `README.md`.
- **Replace-Import Safety Backup Guarantee**: Verified that Replace import strictly halts with an error if the safety backup file cannot be created or confirmed on disk, guaranteeing existing notes are never wiped prematurely.
- **Workspace Export, Import & Auto-Registration**: Extended `NoteExportPackage` to include `Workspaces`. On import, custom workspaces are automatically registered into `MainViewModel.Workspaces`, note assignments are remapped, and the active workspace is synchronized so imported notes in custom workspaces are never hidden or stranded.
- **Stable-Only Update Channel**: Updated `UpdateCheckService.cs` to filter out drafts and prereleases (`prerelease: true`, `-beta`, `-rc`, `-alpha`), ensuring regular users are not prompted for unstable releases unless opted in.
- **Cross-Desktop Notification Delivery Reliability**: Upgraded `LinuxNotificationService.cs` to use `ArgumentList` (ensuring POSIX flag order before arguments) with fallback to `kdialog` and `zenity`. Enhanced `WindowsNotificationService.cs` with fallback toast notifier handling.
- **Stale File Clean-Up on Linux Upgrades**: Updated `build-linux.sh` (embedded installer) and `install-linux.sh` to remove obsolete installation files prior to copying new binaries.

### Verification
- `dotnet test tests/PinNote.Tests/PinNote.Tests.csproj` succeeded: 155 passed, 0 failed, 0 skipped.
- `git diff --check` reported 0 whitespace errors.
- Linux package (`PinNote-1.1.0-linux-x64.tar.gz`) and self-extracting installer (`PinNote-Setup-1.1.0.sh`) built cleanly.

## [Version 1.0.4 Release & Update Notification Foundation] - November 14, 2026

### Changes
- **Version 1.0.4 Synchronization**: Synchronized version 1.0.4 across `PinNote.csproj`, Inno Setup configuration (`installer/PinNote.iss`), build scripts (`build-installer.ps1`, `build-linux.sh`, `install-linux.sh`), and `README.md`.
- **Automatic Update Notifications**: Integrated `IUpdateCheckService` and `UpdateCheckService` that checks GitHub releases asynchronously and alerts users via a dashboard notification badge `🔔 Update v{version}`.
- **In-App Quick Guide Refresh**: Updated `AvaloniaInstructionsWindow.axaml` and `InstructionsWindow.xaml` to thoroughly document Workspaces, Reminders, Templates, Markdown mode, Update notifications, and all keyboard shortcuts while preserving the visual theme and styling.
- **Dual Platform Installers**: Built standalone Inno Setup Windows installer (`artifacts/installer/PinNote-Setup-1.0.4.exe`) and Linux self-extracting installer (`artifacts/installer/PinNote-Setup-1.0.4.sh`) alongside the standard `.tar.gz` package.

### Verification
- `dotnet test tests/PinNote.Tests/PinNote.Tests.csproj` succeeded: 147 passed, 0 failed, 0 skipped.

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
