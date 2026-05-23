# Development Log - PinNote

This file tracks the features implemented, changes made, and the rationale behind them.

## [Initial Development Phase] - May 2026

### Features Implemented
- **WPF Application Setup**: Initialized the project as a .NET 8.0 Windows application using WPF and the MVVM pattern.
- **Note Management**: Implemented the ability to create, edit, and delete sticky notes.
- **Persistence (JSON)**: Added `StorageService` to save and load notes from a local `notes.json` file.
    - *Rationale*: Ensures user notes are not lost when the application is closed.
- **Always-on-Top (Pinning)**: Implemented a pinning feature to keep specific notes above all other windows.
    - *Rationale*: Crucial for a sticky note app where some information needs to be constantly visible.
- **System Tray Integration**: Used `H.NotifyIcon` to allow the app to run in the background and minimize to the tray.
    - *Rationale*: Keeps the desktop clutter-free while maintaining quick access to note-taking.
- **Main Dashboard**: Created a central window to list all notes with a search/filter capability.
    - *Rationale*: Provides an overview and easy management of multiple notes.
- **Auto-Startup**: Implemented `StartupService` to manage Windows Registry keys for launching on boot.
    - *Rationale*: Convenience for users who want their notes available immediately upon starting their computer.
- **Customization**: Added properties for font size, opacity, and color for individual notes.

### Project Metadata & Documentation
- **README.md**: Created a comprehensive overview of the project, features, and setup instructions.
    - *Rationale*: Essential for repository clarity and onboarding.
- **.gitignore**: Added a standard .NET and Python ignore file.
    - *Rationale*: Prevents tracking of build artifacts (`bin/`, `obj/`) and local environments (`.venv/`).
- **LICENSE**: Added the MIT License.
    - *Rationale*: Defines the legal terms for software usage and distribution.
- **DEVELOPMENT_LOG.md & GEMINI.md**: Initialized tracking for changes and project instructions.

## [Window Resizing Fix] - May 24, 2026

### Changes
- **Comprehensive Resize Handles**: Added missing resize thumbs for Bottom, Top-Left, Top-Right, Bottom-Left, and Bottom-Right.
- **Persistent Accessibility**: Moved resize handles to a dedicated layer so they remain functional even when the Options/Formatting bars are hidden.
- **Improved Hit Areas**: Increased the width/height of edge and corner handles for easier interaction.
- **Visual Feedback**: Redesigned the bottom-right resize grip to be more consistent with modern Windows aesthetics.

### Rationale
- The previous implementation had disappearing handles and missing edges, making it difficult for users to resize notes reliably. Providing handles on all sides and corners ensures a standard and predictable desktop experience.

---

## Instructions for Updates
- Every time a new feature is added, a bug is fixed, or an architectural change is made, this file **must** be updated.
- Include the **What** (the change) and the **Why** (the rationale).

---

## [System Tray Reliability Fix] - May 2026

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

## [Professional UI Refinement] - May 24, 2026

### Changes
- **Streamlined Context Menu**: Redesigned the note's right-click menu to be more professional and focused.
    - Added **Icons** to all primary actions (Pin, Color, Transparency, Text Size, Delete).
    - Renamed "Stick to desktop" to the more standard **"Always on top"**.
    - Highlighted **"Delete note"** in red to indicate a destructive action.
    - Removed the redundant "New note" option (already available in the top toolbar).
- **Improved Visual Hierarchy**: Used separators and iconography to group related settings, making the menu faster to navigate.

### Verification
- `dotnet build` completed successfully.

---

*Log updated on May 24, 2026.*

