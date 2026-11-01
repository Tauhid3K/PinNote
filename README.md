# PinNote

PinNote is a free, open-source, cross-platform desktop sticky note application built with .NET 8, WPF for Windows, and Avalonia UI for Linux. It provides a reliable way to capture ideas, manage tasks, and keep important information always within reach.

## 🚀 Features

- **Interactive Checklists:** Create, check, uncheck, reorder (up/down), and delete checklist items. Seamlessly switch between plain text and checklist mode.
- **Quick Note Creation:** Instant note creation via global shortcut (`Ctrl+Alt+N`) on Windows/X11 or `pinnote --new-note` command on Linux/Wayland.
- **Reliable Autosave & Recovery:** Debounced atomic JSON writes, `.bak` snapshot recovery, corrupt file preservation, and protection against zero-byte overwrite.
- **Fast Cross-Platform Search & Sorting:** Case-insensitive search with whitespace normalization and multi-word matching across titles, plain text, rich text (FlowDocument XAML), and checklist contents. Flexible dashboard sorting by Title (A-Z, Z-A), Last Modified date, Creation date, or Default insertion order, with pinned notes always prioritized.
- **Backup, Export & Import:** Convenient JSON interchange format (`NoteExportPackage`) with schema versioning, collision protection (reassigning IDs), and automatic pre-import safety backups.
- **Multi-Monitor Window Restoration:** Intelligently restores note positions across multi-monitor configurations, negative coordinates, and handles disconnected monitors by relocating notes to the active primary screen.
- **Always on Top (Pinning):** Pin critical notes to keep them visible above all other windows.
- **Crystal Clear Mode:** A per-note frosted glass transparent mode with customizable text colors and opacity.
- **Quick Duplicate Action:** Duplicate any note with matching contents, colors, size, and checklist items.
- **System Tray Integration:** Native tray integration on Windows and Linux with customizable themes (System, Light, Dark).

## 🛠️ Tech Stack

- **Framework:** [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- **UI Frameworks:**
  - Windows: WPF (Windows Presentation Foundation)
  - Linux: Avalonia UI 11
- **Design Pattern:** MVVM (Model-View-ViewModel)
- **Persistence & Serialization:** [Newtonsoft.Json](https://www.newtonsoft.com/json)

## 📂 Project Structure

- **Models:** Defines the `NoteModel` representing the data structure of a note.
- **ViewModels:** Contains the logic for note management (`NoteViewModel`) and the main application state (`MainViewModel`).
- **Services:**
  - `StorageService`: Handles JSON-based persistence of notes.
  - `StartupService`: Manages Windows registry keys for auto-start functionality.
  - `AppStateService`: Maintains application-wide settings and state.
- **UI:** XAML-based windows and value converters for a rich user interface.

## ⚙️ Installation & Usage

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) is only needed if you want to build from source.
- Installed PinNote users do not need .NET 8. The installer publishes a self-contained app that includes the runtime.

### Running the App
1. Clone the repository:
   ```bash
   git clone https://github.com/your-username/PinNote.git
   ```
2. Navigate to the project directory:
   ```bash
   cd PinNote
   ```
3. Build and run the project:
   ```bash
   dotnet run
   ```

### Installing on Linux

#### Option 1: Standard Installation (Tar / Local)
Installs PinNote locally to `~/.local/share/pinnote`, adds `pinnote` to your PATH, and installs the desktop shortcut and application icon:

```bash
chmod +x ./scripts/install-linux.sh
./scripts/install-linux.sh
```

To uninstall later:
```bash
./scripts/uninstall-linux.sh
```

#### Option 2: Flatpak Installation
If you prefer sandboxed execution via Flatpak:

```bash
# Ensure flatpak-builder is installed (e.g. sudo pacman -S flatpak-builder)
chmod +x ./scripts/build-flatpak.sh
./scripts/build-flatpak.sh
```
Run the Flatpak app via:
```bash
flatpak run org.pinnote.PinNote
```

### Building Portable Tar Package
To build a standalone `.tar.gz` bundle:
```bash
chmod +x ./scripts/build-linux.sh
./scripts/build-linux.sh
```
The archive will be created at:
`artifacts/package/PinNote-1.0.3-linux-x64.tar.gz`

### Creating a Windows Installer
To build a standard Windows setup file with shortcuts, uninstall support, a launch-after-install option, and an embedded runtime:

1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php) if it is not already installed.
2. Run the installer build script:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
   ```
3. The installer EXE will be created under `artifacts\installer`.

The generated installer is self-contained, so the target PC does not need .NET 8 installed.

### Quick Usage Notes
- **Global Shortcut:** Press `Ctrl+Alt+N` on Windows or X11 to instantly create and focus a new note. On Linux/Wayland, bind a custom shortcut (such as `Super+Shift+N` or `Ctrl+Alt+N`) to run `pinnote --new-note`.
- **Checklist Mode:** Click the ☑ button in the note title bar or context menu to switch between freeform text and an interactive checklist. Checklist items can be checked, unchecked, reordered (▲/▼), and deleted.
- **Backup & Import/Export:** In the Dashboard or tray menu, use `Export Notes...` to create a standard JSON backup or `Import Notes...` to restore notes. Existing notes are never overwritten (duplicate IDs are safely reassigned).
- **Search & Sort:** Search in the dashboard filters across titles, text, and checklist items with instant case-insensitive multi-word matching. Use the Settings menu to sort notes by title, creation date, or modification time (pinned notes always remain at the top).
- **Change note color:** Right-click a note → Color → select a palette swatch or click `More colors...` to pick a custom color with the system color dialog.
- **Crystal Clear Mode:** Right-click a note → check `Crystal clear`. The note background becomes transparent and the text uses the selected note color.
- **Duplicate a note:** On the dashboard card, click the duplicate button to create a copy of the note with the same content and styling.
- **Theme controls:** Light/Dark theme buttons are in the dashboard title bar; the System theme is available in the tray menu.
- **Show all notes:** Use the tray menu or dashboard `Show All` to restore every saved note window.
- **Hide notes:** Use the tray menu action `Hide all notes` to hide open note windows while keeping PinNote active in the system tray.
- **Tray behavior:** Minimize or close the dashboard to send it to the system tray; click the tray icon to restore the dashboard.

## 📝 License
This project is licensed under the MIT License - see the LICENSE file for details.
