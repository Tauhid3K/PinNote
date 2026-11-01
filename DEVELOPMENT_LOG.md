# Development Log - PinNote

This file tracks the features implemented, changes made, and the rationale behind them.

### Date: 2026-10-26 (P0 Essential Features Implementation)
Author: Antigravity (assistant)
Files changed:
- Models/ChecklistItemModel.cs (new)
- Models/NoteExportPackage.cs (new)
- Models/NoteModel.cs
- Models/NoteType.cs (new)
- Services/IGlobalShortcutService.cs (new)
- Services/ImportExportService.cs (new)
- Services/IWindowManager.cs
- Services/LinuxGlobalShortcutService.cs (new)
- Services/SearchService.cs (new)
- Services/SingleInstanceService.cs (new)
- Services/StorageService.cs
- Services/WindowRestorationHelper.cs (new)
- Services/WindowsGlobalShortcutService.cs (new)
- UI/MainDashboardWindow.xaml
- UI/NoteWindow.xaml
- UI/NoteWindow.xaml.cs
- UI/WpfWindowManager.cs
- ViewModels/ChecklistItemViewModel.cs (new)
- ViewModels/MainViewModel.cs
- ViewModels/NoteViewModel.cs
- Views/AvaloniaDashboardWindow.axaml
- Views/AvaloniaNoteWindow.axaml
- Views/AvaloniaNoteWindow.axaml.cs
- Views/AvaloniaWindowManager.cs
- App.axaml.cs
- App.xaml.cs
- Program.cs
- PinNote.csproj
- tests/PinNote.Tests/PinNote.Tests.csproj (new)
- tests/PinNote.Tests/AutosaveAndRecoveryTests.cs (new)
- tests/PinNote.Tests/ChecklistTests.cs (new)
- tests/PinNote.Tests/ImportExportTests.cs (new)
- tests/PinNote.Tests/QuickNoteAndSingleInstanceTests.cs (new)
- tests/PinNote.Tests/SearchServiceTests.cs (new)
- tests/PinNote.Tests/WindowRestorationTests.cs (new)

Change summary:
- 1. Reliable Autosave & Crash Recovery:
  - Ensured atomic writes with unique GUID temp files, monotonic backup verification, and corrupt-state protection against zero-byte wiping.
  - Added `FlushPendingEdits()` hook in `IWindowManager` so that note editor contents in WPF and Avalonia windows are flushed before shutdown or immediate saves.
  - Implemented automatic recovery from `notes.json.bak` and corrupt file preservation (`notes.json.corrupt.{timestamp}`).
- 2. Quick Note Creation & Single Instance:
  - Implemented cross-platform `SingleInstanceService` using named Mutex and local IPC (NamedPipe on Windows, Unix domain socket on Linux).
  - Secondary instances pass command-line arguments (e.g., `--new-note`, `--show-dashboard`) to the running primary instance and exit immediately without spawning duplicate processes.
  - Windows: Added `WindowsGlobalShortcutService` registering global `Ctrl+Alt+N` via Win32 `RegisterHotKey` with non-blocking message loop.
  - Linux: Added `LinuxGlobalShortcutService` with X11 key grabbing support and documented CLI fallback (`pinnote --new-note`) for Wayland environments where compositor security disallows client-side global key grabbing.
- 3. Checklists:
  - Added `NoteType` enum and `ChecklistItemModel` with full roundtrip serialization in `NoteModel`.
  - Created `ChecklistItemViewModel` supporting create, edit, toggle check/uncheck, delete, and reorder (up/down).
  - Implemented dynamic note-type conversion: plain text to checklist (auto-detecting `- [ ]`, `[x]`, bullets, lines) and checklist back to text.
  - Added interactive checklist UI in both Avalonia (`AvaloniaNoteWindow.axaml`) and WPF (`NoteWindow.xaml`).
- 4. Reliable Cross-Platform Search:
  - Built `SearchService` with case-insensitive matching, whitespace normalization, and multi-term queries matching words across title and content.
  - Extracted clean text from FlowDocument XML/XAML, handling tags, paragraph breaks, and malformed XML safely without exceptions.
  - Included checklist item text and completion status in searchable index.
  - Added caching with automatic invalidation in `NoteViewModel` to prevent UI stutter on large collections.
- 5. Backup, Import, and Export:
  - Built `ImportExportService` with versioned `NoteExportPackage` schema and support for raw backup arrays.
  - Duplicate identifier protection: reassigns GUIDs on collision to guarantee existing notes are never overwritten.
  - Pre-import snapshot backup: automatically creates timestamped backup before bulk import operations.
  - Added Import, Export, and Backup buttons and menus in both Windows and Linux dashboards and tray menus.
- 6. Multi-Monitor and Window Restoration:
  - Built `WindowRestorationHelper` supporting negative coordinates (screens placed to the left or above primary) and virtual desktop boundaries.
  - Disconnected monitor fallback: safely detects unmapped coordinates and relocates notes to active primary screen.
  - Dimension and screen edge clamping ensuring note title bars are always visible and accessible.
- 7. Vector Icon Geometry for Linux UI (Avalonia):
  - Replaced all unicode emoji text glyphs (`📌`, `🎨`, `📄`, `🗑️`, `📥`, `📤`, `💾`, `☑`, `🪟`) with scalable vector `Path` geometry across `AvaloniaDashboardWindow.axaml`, `AvaloniaNoteWindow.axaml`, `AvaloniaInstructionsWindow.axaml`, and `ConfirmDeleteDialog.axaml`.
  - Dynamically bound icon brushes to `CardTextColor`, `HeaderIconColor`, and `SystemControlForegroundBaseHighBrush` so icons remain crisp, high-contrast, and independent of system emoji font packages.
- 8. Title Bar Drag Region & Text Formatting Toolbar:
  - Resized and constrained Title `TextBox` (`MinWidth="70"`, `MaxWidth="115"`) so that it fits titles and watermarks without monopolizing the title bar width.
  - Added dedicated middle draggable area (`Cursor="SizeAll"`, `ToolTip="Drag to move note"`) allowing effortless window dragging across all window sizes without accidentally triggering title text focus.
  - Added bottom formatting toolbar in Avalonia text mode matching the WPF formatting bar: Bold (`B`, `Ctrl+B`), Italic (`I`, `Ctrl+I`), Underline (`U`, `Ctrl+U`), Bullet list toggle (`•`), and Image insertion.
- Automated Test Suite:
  - Added 35 xUnit tests in `PinNote.Tests` covering autosave, recovery, checklists, search, import/export, IPC, and window restoration.

### Date: 2026-10-26 (Single-Path Save & Concurrency Elimination)
Author: Antigravity (assistant)
Files changed:
- Models/NoteModel.cs
- Services/StorageService.cs
- ViewModels/MainViewModel.cs

Change summary:
- Deadlock Elimination & Single-Path Execution:
  1. Removed `_saveLock` completely from `MainViewModel.cs`. Previously, the debounce timer thread held `_saveLock` while synchronously dispatching to the UI thread, which risked a lock-inversion deadlock if the UI thread attempted to save concurrently.
  2. Dispatched the entire save routine (`PerformSave`) directly onto the UI thread via `IWindowManager.RunOnUiThread`.
- Mutation-Safe Snapshot & Serialization:
  1. Added a `Clone()` method to `NoteModel.cs` to deep-copy note properties.
  2. Moved both model cloning/snapshotting and JSON serialization (`JsonConvert.SerializeObject`) onto the UI thread in `PerformSave()`. Because the UI thread executes sequentially, user edits or collection changes can never overlap with snapshotting or serialization.
  3. Added `SaveNotesJson(string json, int noteCount)` to `StorageService.cs`, enabling persistence directly from the immutable serialized JSON string without touching or referencing mutable UI models on disk write.

### Date: 2026-10-26 (Transparency, Deletion Confirmation, Rich Text & Platform Refinements)
Author: Antigravity (assistant)
Files changed:
- ViewModels/NoteViewModel.cs
- ViewModels/MainViewModel.cs
- Views/AvaloniaNoteWindow.axaml
- Views/AvaloniaNoteWindow.axaml.cs
- Views/AvaloniaDashboardWindow.axaml
- Views/AvaloniaDashboardWindow.axaml.cs
- Views/AvaloniaWindowManager.cs
- Views/ConfirmDeleteDialog.axaml
- Views/ConfirmDeleteDialog.axaml.cs
- Services/IWindowManager.cs
- UI/WpfWindowManager.cs
- App.axaml.cs
- Services/AvaloniaTrayService.cs
- PinNote.csproj
- scripts/install-linux.sh

Change summary:
- Crystal Clear Transparency Control:
  1. Updated `EffectiveBodyColor` and `EffectiveTitleBarColor` in `NoteViewModel.cs` so that when `IsCrystalClear` is active, note transparency (`Opacity`) directly adjusts the frosted glass background alpha level.
  2. Updated `EffectiveTextColor` to apply `TextOpacity`, allowing granular transparency control for both note body and text in Crystal Clear mode.
  3. Added high-contrast fallback for `HeaderIconColor` and close `✕` button so icons never blend into transparent notes or dark backgrounds.
- Visual Color Swatch Grid & Custom Color Picker:
  1. Added clickable color swatch button grids for both Note Color and Text Color across note context menus and flyouts.
  2. Preserved the "Custom Color..." `ColorPicker` allowing arbitrary RGB/Hex selection from swatches and palette.
- Modal Deletion Confirmation Dialog:
  1. Created `ConfirmDeleteDialog.axaml` and `ConfirmDeleteDialog.axaml.cs`.
  2. Added `ConfirmDeleteAsync(string noteTitle)` and `RunOnUiThread(Action action)` to `IWindowManager`, `AvaloniaWindowManager`, and `WpfWindowManager`.
  3. Routed note deletion through asynchronous confirmation in `MainViewModel.cs`.
- Cross-Platform Rich Text Preservation:
  1. Updated `LoadContent()` in `AvaloniaNoteWindow.axaml.cs` to preserve paragraph structures and `<LineBreak/>` as newlines instead of squashing into spaces.
  2. Kept `_originalRawContent` intact if note content was unedited.
  3. Re-wrapped edited plain text into standard FlowDocument XML tags on save for full Windows WPF compatibility.
- Dashboard Card Click Propagation Fix:
  1. In `AvaloniaDashboardWindow.axaml.cs`, inspected event source in `NoteCard_PointerPressed` to ensure clicks on Duplicate or Delete buttons do not inadvertently trigger opening the note window.
  2. Restored 📌 pushpin brand emoji in the dashboard navigation header.
  3. Bound note card preview to `PlainTextPreview` and `CardTextColor` for crisp readability.
- Linux Startup Preferences:
  1. Updated `App.axaml.cs` to obey saved settings `GetShowUiOnStartup()` and `GetStartMinimized()`.
- Thread-Safe Save Debounce:
  1. Marshalled `_notes` snapshot creation in `SaveNotesImmediately` to the UI thread via `IWindowManager.RunOnUiThread` to prevent background thread collection modification race conditions.
- Top-Bar Tray Icon Support:
  1. Packaged and installed GNOME AppIndicator extension (`appindicatorsupport@rgcjonas.gmail.com`) to `~/.local/share/gnome-shell/extensions` and enabled in `gsettings`.
  2. Enhanced `AvaloniaTrayService.cs` with fallback to `AssetLoader.Open("avares://PinNote/Assets/icon.png")`.
  3. Updated `scripts/install-linux.sh` to install icons to `~/.local/share/pixmaps/pinnote.png` and standard hicolor paths with `Icon=pinnote`.

### Date: 2026-10-26 (Follow-up Fixes - Screenshot 08-49-28)
Author: Antigravity (assistant)
Files changed:
- Views/AvaloniaDashboardWindow.axaml
- Views/AvaloniaNoteWindow.axaml
- ViewModels/NoteViewModel.cs
- Program.cs
- scripts/install-linux.sh

Change summary:
- Dashboard Card Contrast & Visibility Fix:
  1. Updated note card title, preview body, and footer in `AvaloniaDashboardWindow.axaml` to bind to `TextColor` (dynamic high-contrast black/white) instead of `EffectiveTextColor`. White custom text chosen for glass notes now displays as crisp black text on bright yellow cards, eliminating the invisible text bug.
- Restored Signature PinNote Brand Icon in Dashboard:
  1. Replaced the generic red pushpin emoji (`📌`) in the dashboard navigation bar with the official PinNote logo image (`Assets/icon.png`).
- Fixed Title Bar Controls Contrast & Icon Opacity:
  1. Updated `HeaderIconColor` in `NoteViewModel` to respect `CustomTextColor` when set, ensuring that when the title is white, the close button `✕` is also crisp white (not black).
  2. Increased unpinned `PinOpacity` from 0.45 to 0.70 to avoid dark muddy smudges against dark backgrounds.
- Enhanced Linux Wayland / X11 Window Transparency & Composition:
  1. Configured `ExtendClientAreaToDecorationsHint="True"`, `ExtendClientAreaChromeHints="NoChrome"`, and `ExtendClientAreaTitleBarHeightHint="-1"` in `AvaloniaNoteWindow.axaml` to ensure the compositor allocates an extended 32-bit ARGB surface without dark fallback boxes.
  2. Configured `X11PlatformOptions` rendering modes (Glx, Egl, Software) in `Program.cs`.
- Process Hygiene:
  1. Added automatic termination of stale background `PinNote` instances in `scripts/install-linux.sh` to ensure updates take effect immediately.

### Date: 2026-10-26
Author: Antigravity (assistant)
Files changed:
- Views/AvaloniaNoteWindow.axaml
- Views/AvaloniaNoteWindow.axaml.cs
- ViewModels/NoteViewModel.cs
- ViewModels/MainViewModel.cs
- Services/StorageService.cs
- Services/IWindowManager.cs
- Views/AvaloniaWindowManager.cs
- UI/WpfWindowManager.cs
- PinNote.csproj
- scripts/install-linux.sh

Change summary:
- Fixed Crystal Clear mode:
  1. Renamed menu item to strictly "Crystal clear" (removed parentheses).
  2. Fixed double-toggle bug caused by simultaneous `Command` invocation and `IsChecked` two-way binding on `ToggleType="CheckBox"`.
  3. Added `TransparencyBackgroundFallback="Transparent"` to `AvaloniaNoteWindow.axaml` ensuring Linux compositors properly render client alpha without fallback solid backgrounds.
- Fixed Title Bar Icons visibility:
  1. Pin icon now binds `Opacity="{Binding PinOpacity}"` (1.0 when pinned, 0.45 when unpinned; never 0.0).
  2. Close button uses solid `Foreground="{Binding HeaderIconColor}"` and added interactive hover styles (`.close-btn:pointerover` / `.title-btn:pointerover`) so title controls remain clearly visible regardless of note background color or transparency level.
- Fixed App Drawer Icon in Linux:
  1. Updated `scripts/install-linux.sh` to install the signature PinNote icon into all standard hicolor dimensions (`16x16`, `32x32`, `48x48`, `64x64`, `128x128`, `256x256`).
  2. Configured `pinnote.desktop` with an absolute icon path to bypass stale desktop theme caches and immediately refresh the GNOME/KDE app drawer.
- Debounced Window Geometry & Autosave:
  1. Added `_geometryTimer` in `AvaloniaNoteWindow.axaml.cs` so moving or resizing windows does not serialize and rewrite `notes.json` on every pixel change.
  2. Added `UpdateBounds` in `NoteViewModel` to batch position and size changes into a single notification.
  3. Added cross-platform debounced `QueueSave()` with `System.Timers.Timer` in `MainViewModel`, while saving immediately on note creation, deletion, duplication, or app shutdown.
- Storage Failure Visibility:
  1. Added `SaveFailed` and `LoadFailed` events in `StorageService`.
  2. Trigger desktop notification alerts via `NotificationService` and error dialogs via `IWindowManager.ShowErrorMessage` if saving or loading notes fails.
- Project Architecture Cleanup:
  1. Cleaned up `PinNote.csproj` conditional compiles into clean directory-based groupings (`UI\**`, `Themes\**` vs `Views\**`, `Services\Avalonia*`).

Reason:
- User reported issues with Crystal Clear mode, missing title bar icons on transparent notes, missing app drawer icon update, excessive disk writes on note move/resize, silent storage errors, and platform split maintainability.


### Date: 2026-10-25
Author: Antigravity (assistant)
Files changed:
- Services/IWindowManager.cs (added)
- UI/WpfWindowManager.cs (added)
- ViewModels/RelayCommand.cs
- ViewModels/NoteViewModel.cs
- ViewModels/MainViewModel.cs
- App.xaml.cs
- Program.cs (added)
- App.axaml (added)
- App.axaml.cs (added)
- Views/AvaloniaNoteWindow.axaml (added)
- Views/AvaloniaNoteWindow.axaml.cs (added)
- Views/AvaloniaDashboardWindow.axaml (added)
- Views/AvaloniaDashboardWindow.axaml.cs (added)
- Views/AvaloniaInstructionsWindow.axaml (added)
- Views/AvaloniaInstructionsWindow.axaml.cs (added)
- Views/AvaloniaWindowManager.cs (added)
- PinNote.csproj
- scripts/build-linux.sh
- Services/StartupService.cs
- scripts/install-linux.sh (added)
- scripts/uninstall-linux.sh (added)
- scripts/build-flatpak.sh (added)

Change summary:
- Completed cross-platform UI architecture supporting both Windows (WPF/net8.0-windows) and Linux (Avalonia/net8.0):
  1. Decoupled ViewModels from platform-specific UI through new `IWindowManager` interface.
  2. Implemented `WpfWindowManager` for Windows WPF build to preserve 100% of existing Windows behavior and installer pipeline.
  3. Made `RelayCommand` cross-platform, conditionally supporting `CommandManager.RequerySuggested` under Windows and standard events on Linux.
  4. Decoupled `NoteViewModel` from `System.Windows.Media`, using pure hex color parsing for cross-platform support while preserving WPF text brushes.
  5. Implemented complete Avalonia UI suite for Linux: `Program.cs` entry point, `AvaloniaApp` with FluentTheme, `AvaloniaNoteWindow` with dragging and debounced autosave, `AvaloniaDashboardWindow` with card grid and search, `AvaloniaInstructionsWindow`, and `AvaloniaWindowManager`.
  6. Configured `PinNote.csproj` with smart multi-targeting: defaults to `net8.0` on Linux/macOS and multi-targets `net8.0` and `net8.0-windows` on Windows with clean file exclusions.
  7. Updated `scripts/build-linux.sh` to package self-contained executable, desktop launcher, and assets into `PinNote-1.0.3-linux-x64.tar.gz`.
  8. Created Linux installation and uninstallation scripts (`scripts/install-linux.sh`, `scripts/uninstall-linux.sh`) and Flatpak build script (`scripts/build-flatpak.sh`).
  9. Added support for GNOME/Linux top bar system tray icon with crisp pin iconography (`Assets/icon.png`), and added persistent user settings to toggle top-bar system tray icon visibility and bottom-bar taskbar/dock presence.
  10. Fixed typing background issue in Avalonia note editor by overriding `TextBox` `:focus` and `:pointerover` template styles to remain transparent.
  11. Restored full Crystal Clear (Glass) theme mode with `TransparencyLevelHint="Transparent,AcrylicBlur"` and transparent backgrounds for frameless glass aesthetic.
  12. Restored comprehensive transparency levels (100% down to 20%), font sizes, color palettes, and right-click ContextMenu matching Windows PinNote.

Reason:
- User requested complete cross-platform functionality so the application works seamlessly on both Windows and Linux, and can be installed via tarball or Flatpak.


### Date: 2026-06-01
Author: GitHub Copilot (assistant)
Files changed:
- ViewModels/NoteViewModel.cs
- ViewModels/MainViewModel.cs
- UI/MainDashboardWindow.xaml
- README.md
- installer/PinNote.iss
- scripts/build-installer.ps1

Change summary:
- Added a duplicate-note action to dashboard cards so users can quickly copy an existing note with the same content and styling.
- Updated the README and installer versioning to reflect the new release.

Reason:
- The user asked for a duplicate/copy button in the main dashboard and requested corresponding README and installer updates.

Notes / Testing:
- `dotnet build PinNote.csproj /p:UseAppHost=false` succeeded.

### Date: 2026-05-25
Author: GitHub Copilot (assistant)
Files changed:
- ViewModels/MainViewModel.cs
- App.xaml.cs
- README.md
- DEVELOPMENT_LOG.md
- CODEX.md
- GEMINI.md
- COPILOT.md

Change summary:
- Updated the "Show all notes" action so it restores every saved note window, including notes that are saved but not currently visible.
- Updated the project markdown files to describe the restored-note behavior and keep the logs aligned with the current implementation.

Reason:
- The user clarified that "Show all notes" should surface the remaining saved notes, not just the dashboard summary or already open windows.

Notes / Testing:
- `dotnet build` succeeded after the behavioral change. A file-lock warning only appeared when PinNote was still running during validation.

## [Initial Development Phase] - May 2026

### Features Implemented
    - *Rationale*: Ensures user notes are not lost when the application is closed.
    - *Rationale*: Crucial for a sticky note app where some information needs to be constantly visible.
    - *Rationale*: Keeps the desktop clutter-free while maintaining quick access to note-taking.
    - *Rationale*: Provides an overview and easy management of multiple notes.
    - *Rationale*: Convenience for users who want their notes available immediately upon starting their computer.

### Project Metadata & Documentation
- **README.md**: Created a comprehensive overview of the project, features, and setup instructions.
    - *Rationale*: Essential for repository clarity and onboarding.

### Changes
- **Improved Hit Areas**: Increased the width/height of edge and corner handles for easier interaction.
- **Visual Feedback**: Redesigned the bottom-right resize grip to be more consistent with modern Windows aesthetics.
### Rationale
- The previous implementation had disappearing handles and missing edges, making it difficult for users to resize notes reliably. Providing handles on all sides and corners ensures a standard and predictable desktop experience.
---

- Every time a new feature is added, a bug is fixed, or an architectural change is made, this file **must** be updated.



### Bug Fixed
- **Minimize to Tray**: Replaced the XAML-owned `H.NotifyIcon` tray implementation with a code-owned `System.Windows.Forms.NotifyIcon`.
    - *Rationale*: The dashboard could hide on minimize without reliably showing a usable system tray icon. The Windows Forms tray icon uses the direct Windows notification area API and gives the app a dependable restore path.

### Behavior Changes
- **Dashboard Minimize/Close**: Minimize and close now hide the dashboard to the tray instead of closing the dashboard window.
    - *Rationale*: Prevents the app from becoming difficult to restore after the dashboard is hidden.
- **Tray Menu**: Added code-driven tray menu actions for new note, show all notes, hide all notes, bring notes on top, and exit.
    - *Rationale*: Keeps the app accessible from the system tray even when all windows are hidden.
- **Removed Old XAML Tray Menu**: Removed the unused `TrayMenu` resource from `App.xaml`.
    - *Rationale*: The active tray menu now lives in `App.xaml.cs`, so the old XAML menu would be confusing stale code.

### Project Changes
- **Windows Forms Enabled**: Enabled `<UseWindowsForms>true</UseWindowsForms>` in the WPF project.
    - *Rationale*: Required for `System.Windows.Forms.NotifyIcon`.
- **Implicit Usings Disabled**: Changed implicit usings from enabled to disabled.
    - *Rationale*: Prevents WPF and WinForms namespace collisions.
- **Removed H.NotifyIcon.Wpf**: Removed the unused tray package reference.
    - *Rationale*: The app no longer uses that package and the build warning about transitive package resolution is gone.

### Verification
- `dotnet build` completed successfully with 0 warnings and 0 errors.

---

## [Note Window Wireframe Layout] - May 2026

### UI Changed
- **Note Window Layout**: Reworked the sticky note UI to match the requested wireframe with a top options row, editable title row, main note body, bottom formatting toolbar, and bottom-right resize handle.
    - *Rationale*: Keeps the note controls visible and makes the note easier to understand at a glance.
- **Always-Visible Controls**: The top row now shows new note, pin, settings, and close controls without waiting for focus.
    - *Rationale*: Improves discoverability and follows the requested layout.
- **Bottom Toolbar**: The formatting toolbar now stays visible and uses `B`, `I`, `U`, `*`, and `img`.
    - *Rationale*: Matches the sketch and keeps writing tools easy to access.

### Defaults Changed
- **Default Note Colors**: New notes now use a sky-blue header and white body with full opacity.
    - *Rationale*: The requested default color was white or sky blue.
- **Two-Tone Color Support**: The note model now allows the title/header color and body color to stay different.
    - *Rationale*: Required for the sky-blue header plus white note body design.

### Verification
- `dotnet build` completed successfully with 0 warnings and 0 errors.

---

## [Tray Fallback and Idle Note Chrome] - May 2026

### Bug Fixed
- **Taskbar Fallback**: If the tray icon cannot be created or confirmed, minimizing the dashboard now leaves it minimized in the Windows taskbar instead of hiding it completely.
    - *Rationale*: Prevents the app from becoming unreachable when tray behavior fails.

### UI Changed
- **Idle Note Chrome**: The top options row and bottom formatting toolbar hide after a short idle delay and reappear on mouse or keyboard activity.
    - *Rationale*: Keeps notes visually clean while preserving quick access to controls.
- **Draggable Title Row**: The title row can still be used to drag the note when the top options row is hidden.
    - *Rationale*: Maintains usability in idle mode.
- **Darker Header/Toolbar Color**: The note header and toolbar now use a darker shade derived from the selected note color.
    - *Rationale*: Gives every color choice clearer visual hierarchy.
- **Toolbar Polish**: Improved spacing and replaced rough list/image text controls with cleaner icon-style buttons.
    - *Rationale*: Makes the lower formatting bar feel more professional.

### Verification
- `dotnet build` completed successfully with 0 warnings and 0 errors.

---

## [Note Toolbar Layout Refinement] - May 2026

### UI Changed
- **Removed Drag Icon**: Removed the three-line icon from the note options bar.
    - *Rationale*: It looked like a menu but was only a visual drag marker, which made the UI confusing.
- **Bottom Toolbar Position**: Moved the format bar flush to the bottom of the note.
    - *Rationale*: Keeps formatting controls anchored where users expect them.
- **Responsive Toolbar Layout**: Changed the toolbar buttons to use proportional grid columns.
    - *Rationale*: Buttons keep balanced spacing when the note is resized.
- **Resize Grip**: Reworked the bottom-right resize handle into a clearer visible grip.
    - *Rationale*: Makes resizing easier to discover and use.
- **Color Palette Grid**: Changed the color palette from a flowing row to a fixed 6-column box grid.
    - *Rationale*: Makes color selection more organized and compact.

## [UI Cleanliness Fix] - May 24, 2026

### Changes
- **Streamlined Minimize Notifications**: Re-enabled the tray balloon notification ("Running in system tray") while keeping the custom fallback window disabled.
    - *Rationale*: Users wanted a subtle "notify only" confirmation that the app is still running in the tray, without the intrusiveness of an extra popup window.
- **Removed Startup Notification**: Disabled the "Welcome to PinNote" tray balloon on application launch.
    - *Rationale*: Provides a cleaner, silent startup experience.

## [Scaling and Dashboard Resizing Fix] - May 24, 2026

### Changes
- **Unified Font Scaling**: Updated `NoteViewModel` and `NoteWindow` so that text size settings apply equally to the title and body.
    - *Rationale*: Users preferred the title to match the body's font size while remaining bold, rather than being scaled differently.
- **Extra Small Text Option**: Added an "Extra Small" (12px) font size setting to the note context menu.
    - *Rationale*: Provides more flexibility for ultra-compact notes.
- **Resizable Dashboard**: Enabled `CanResizeWithGrip` for the `MainDashboardWindow` and set sensible `MinWidth`/`MinHeight` constraints.
    - *Rationale*: Allows users to adjust the dashboard size to fit their screen and view more notes at once.

## [Ultra-Small Responsive Notes] - May 24, 2026

### Changes
- **Reduced Size Constraints**: Lowered `MinWidth` and `MinHeight` of note windows from 260x220 to 100x100.
    - *Rationale*: Fulfills the user request to allow notes to be as small as possible.
- **Responsive Toolbar Layering**: Wrapped the top Options bar and bottom Formatting bar in horizontal ScrollViewers.
    - *Rationale*: Prevents UI buttons from squashing or overlapping when the window is very narrow. The bars now clip gracefully instead of breaking the layout.
- **Improved Alignment**: Reduced padding and minimum button widths for better space efficiency in small notes.

## [Persistence and Dashboard Improvements] - May 24, 2026

### Changes
- **Persistent Note Restoration**: Updated `LoadNotes` to automatically open all saved note windows in their last saved positions upon application startup.
    - *Rationale*: Users expect their sticky notes to be immediately available and positioned correctly when they restart the app or their computer.
- **Dedicated Dashboard View**: Split the `ShowAllNotes` logic so that the "Show all notes" tray menu option and tray icon click now open **only** the dashboard window.
    - *Rationale*: Provides a cleaner way to manage notes without forcing all individual note windows to the front if the user only wants to see the summary dashboard.
- **Enhanced Startup Logic**: Ensured that all note windows are restored even if the main dashboard is set to start minimized or hidden.

### Verification
- `dotnet build` completed successfully.

## [Window Dragging Fix] - May 24, 2026

### Changes
- **Preview Event Dragging**: Switched to `PreviewMouseLeftButtonDown` for the `OptionsBar` and `Title` border.
    - *Rationale*: Standard `MouseLeftButtonDown` was being consumed by internal controls (like the `ScrollViewer` or `TextBox`), preventing the window from being moved. Using the preview event allows the window to intercept the drag action first.
- **Dedicated Grab Area**: Added a transparent `Border` in the `OptionsBar`'s empty space to act as a reliable "handle" for dragging, even when many buttons are present.
- **Improved Hit Testing**: Updated the dragging logic to explicitly ignore clicks on buttons and text boxes, ensuring they still function while keeping the rest of the header draggable.

### Verification
- `dotnet build` completed successfully.

## [Body Text Transparency Fix] - May 24, 2026

### Changes
- **Enforced Body Transparency**: Updated `SyncEditorColors` to explicitly apply the `TextBrush` (which contains the transparency alpha) to the entire `RichTextBox` document range.
    - *Rationale*: Previously, internal formatting in the note body could prevent text from inheriting transparency changes. Applying it to the full `TextRange` ensures the body text matches the title text's transparency.

## [UI Visual Refinements] - May 24, 2026

### Changes
- **Centered Note Titles**: Updated the title `TextBox` in `NoteWindow` to use `TextAlignment="Center"`.
    - *Rationale*: Provides better visual balance and matches the user's preference for centered headers.
- **Cleaner Taskbar**: Set `ShowInTaskbar="False"` for the Instructions window.
    - *Rationale*: Keeps the Windows taskbar focused on primary applications by hiding utility/instruction windows.
- **Dashboard Persistence**: Implemented size and position memory for the Main Dashboard.
    - *Rationale*: Professional software should remember user layout preferences. The dashboard now restores its exact last-used dimensions and location upon restart.

## [Dedicated Instruction Window] - May 24, 2026

### Changes
- **Professional Instruction Window**: Replaced the "Welcome" sticky note with a dedicated `InstructionsWindow`.
    - *Rationale*: Fulfills the user request for a cleaner, non-note based instruction display. A dedicated window is more professional and prevents the user's notes collection from being cluttered with onboarding text.
- **Unified Help Logic**: Updated the Dashboard's Info ("i") icon and the first-run onboarding to launch this new window instead of creating a note.
- **Improved Content Layout**: Designed the new window with clear sections for "The Essentials," "Shortcuts," and "Dashboard" usage.

### Verification
- `dotnet build` completed successfully.

## [Professional UI Refinement] - May 24, 2026

### Changes
- **Streamlined Context Menu**: Redesigned the note's right-click menu to be more professional and focused.
    - Added **Icons** to all primary actions (Pin, Color, Transparency, Text Size, Delete).
    - Renamed "Stick to desktop" to the more standard **"Always on top"**.
    - Highlighted **"Delete note"** in red to indicate a destructive action.
    - Removed the redundant "New note" option (already available in the top toolbar).
- **Improved Visual Hierarchy**: Used separators and iconography to group related settings, making the menu faster to navigate.

## [Professional Onboarding] - May 24, 2026

### Changes
- **Welcome Note Onboarding**: Implemented a professional first-run experience that creates a pre-formatted "Welcome to PinNote" note.
    - *Rationale*: Provides immediate value to professional users by explaining key features (shortcuts, pinning, resizing) in a clean, visual format right after installation.
- **On-Demand Instructions**: Added a professional **"i" (Info)** icon to the Dashboard header.
    - *Rationale*: Allows users to quickly restore the "Welcome" note at any time if they need to review the instructions or keyboard shortcuts.
- **Visual Polish**: Refined the onboarding content with better XAML formatting, improved spacing, and consolidated shortcut lists. Fixed layout jumps by standardizing toolbar row heights.

## [Exit Confirmation] - May 24, 2026

### Changes
- **Dashboard Exit Prompt**: Updated the main dashboard's "X" (close) button to show a professional confirmation dialog instead of minimizing to the tray.
    - Added a helpful tip to the prompt suggesting that users minimize the app if they want to keep it running in the system tray.
    - *Rationale*: Users specifically requested a confirmation prompt to prevent accidental application shutdown, while ensuring they know how to keep it running in the background.

### Verification
- `dotnet build` completed successfully.

### Verification
- `dotnet build` completed successfully.

## [P0 Cross-Platform Product Hardening] - October 29, 2026

### Changes
- **Sorting in Dashboard (P0.4)**:
    - Added `NoteSortMode` enum supporting Title (A-Z, Z-A), Last Modified (Newest/Oldest), Creation Date (Newest/Oldest), and Default insertion order.
    - Updated `MainViewModel` with `SortedFilteredNotes`, ensuring pinned notes always stay grouped at the top.
    - Persisted user's sort preference in `AppStateService` (`SortMode`).
    - Added interactive Sort menu to `AvaloniaDashboardWindow.axaml`.
- **Note Timestamps (P0.1, P0.4)**:
    - Added `CreatedAt` and `ModifiedAt` (UTC) to `NoteModel` and `NoteViewModel`.
    - Automatically updated `ModifiedAt` on title and content edits.
    - Backward-compatible migration ensures older notes load seamlessly without loss of data.
- **Asynchronous Save Protection (P0.1)**:
    - Added `snapshotTimestamp` guard in `StorageService.SaveNotesJson` to prevent stale delayed saves from overwriting newer content.
- **Wayland Shortcut Fallback Notice (P0.2)**:
    - Detected Wayland environment when global shortcut registration cannot grab keys.
    - Added a graceful one-time notification advising users to bind `pinnote --new-note` in desktop keyboard settings, tracked via `AppStateService.ShouldShowWaylandShortcutNotice()`.
- **Dashboard Position Restoration (P0.6)**:
    - Enhanced `AvaloniaDashboardWindow` to restore saved bounds and position on launch using `WindowRestorationHelper.SanitizeBounds()`, and persist bounds upon closing or hiding.
- **Safe Export Atomicity (P0.5)**:
    - Updated `ImportExportService.ExportNotes` to write to a temporary file before atomic rename, preventing truncated exports.
- **Comprehensive Automated Test Coverage (P0.1-P0.6)**:
    - Added `SortingTests.cs` (13 tests): verifies default pinned grouping, alphabetical ordering, date sorting, combined search+sort, and timestamp tracking.
    - Added `StorageEdgeCaseTests.cs` (8 tests): verifies both-corrupt handling, crash safety guard, concurrent saves, atomic writes, stale snapshot rejection, and timestamp backward compatibility.
    - Added `MigrationTests.cs` (9 tests): verifies backward compatibility of legacy notes (omitted NoteType, ChecklistItems, Timestamps, Opacity, IsVisible).
    - Added lifecycle and Wayland detection tests in `QuickNoteAndSingleInstanceTests.cs`.
    - Total test suite now stands at 69 automated tests passing (0 failures).

### Verification
- `dotnet test tests/PinNote.Tests/`: 69 passed, 0 failed.
- `dotnet build -c Release`: 0 warnings, 0 errors.

---

*Log updated on October 29, 2026.*

