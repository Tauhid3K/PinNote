# Development Steps Log (Copilot)

This file records each substantive change made to the repository, why the change was made, and any relevant notes or instructions. It is intended to be updated whenever code or configuration is changed so the project's history is clear for maintainers.

Format for entries:
- Date: YYYY-MM-DD
- Author: (assistant or user)
- Files changed: (list of relative paths)

---
## Entries

Author: GitHub Copilot (assistant)
Files changed:
- Services/AppStateService.cs
- UI/MainDashboardWindow.xaml
- UI/NoteWindow.xaml
- Themes/Light.xaml (added)
- Themes/Dark.xaml (added)
- Modified hide behavior so "Hide all notes" hides only individual note windows and keeps the main dashboard visible.
- Added application theme support (System / Light / Dark), persisted via `AppStateService` and applied at startup.
- Added in-dashboard Theme selector (ComboBox) bound to `MainViewModel.ThemeMode`.
- Created `Themes/Light.xaml` and `Themes/Dark.xaml` resource dictionaries with base brushes.
- Increased note option bar icon/button sizes via `NoteWindow.xaml` style changes.
- Exposed `App.SetTheme` as public so the app can apply theme changes from `MainViewModel`.
Reason:
- User requested that "hide all notes" should not hide the main dashboard window; the previous behavior hid both the dashboard and note windows. The change keeps the dashboard visible while hiding notes, matching the user's expectation.
- User requested a dark mode / theme option with a System setting. This was implemented to allow users to choose Light, Dark, or follow the OS preference (read from Windows registry AppsUseLightTheme).
- Adding a theme selector in the dashboard gives an in-app way to change theme immediately (complements the tray submenu).
- Increasing option bar icons improves visibility and usability per user request.

Notes / Testing:
- I attempted to run the app to verify behavior. The running `PinNote.exe` instance locked the build output, so I stopped any running instance and rebuilt.
- To run locally, use:

---

Files changed:
- ViewModels/NoteViewModel.cs

Change summary:
- Updated crystal-clear behavior: when `IsCrystalClear` is enabled, changing a note's color now updates the text color to the selected color (previously it always forced white). The background remains transparent in crystal-clear mode.


- Build verified. Pick a color from the `More colors...` dialog or the palette while `Crystal clear` is enabled — the note text will change to that color immediately.

### Date: 2026-05-24
Author: GitHub Copilot (assistant)
- UI/MainDashboardWindow.xaml
- UI/InstructionsWindow.xaml
Change summary:
- Improve dark mode visuals: added a semi-transparent overlay for note preview cards in the dashboard when `ThemeMode` is `Dark`, and updated the `InstructionsWindow` to use theme resources instead of hard-coded white backgrounds.
Reason:
- In Dark mode some UI areas (note previews and instruction dialog) remained bright and jarring. The overlay darkens preview cards for better contrast and the `InstructionsWindow` colors now adapt to the selected theme.
Notes / Testing:
- Restart the app and switch to Dark to observe preview card overlay and the instructions dialog adapting to theme brushes.


## Template for future updates
When making a change, append an entry to this file using the format above. If you want me to update this file automatically after I make changes, I will do so on every change I perform in the repository when you ask or when I make edits as part of a task.

If you'd like automation (a script or git hook) to append entries automatically on commits, I can add a simple `scripts/log_change.ps1` that will append a templated entry; tell me if you want that and what metadata to include (e.g., git author, commit hash).

---

### Date: 2026-05-24
Author: GitHub Copilot (assistant)
Files changed:
- UI/NoteWindow.xaml
- UI/NoteWindow.xaml.cs
- ViewModels/NoteViewModel.cs
- Models/NoteModel.cs

Change summary:
- Removed the `Pick color from screen` eyedropper menu item and handler; the feature no longer appears in the note color menu.
- Changed `More colors...` so clicking it opens the system color dialog directly (no nested menu). Selected colors apply to the note but are NOT added to the main palette.
- Added a `IsCrystalClear` flag on `NoteModel`/`NoteViewModel` and XAML DataTriggers so when enabled the note body and title area become transparent and text is forced white (respecting opacity). This implements a "crystal clear" mode where only white text is shown regardless of picked color.

Reason:
- The user asked that the screen eyedropper option be removed and that `More colors...` immediately show the color dialog. They also requested a crystal-clear mode where only white text is visible; this change implements that behavior so the UI matches the user's intent.

Notes / Testing:
- Verified `dotnet build` succeeds after changes.
- The color dialog now only applies the chosen color to the current note and does not modify the persistent color palette. If you want an explicit "Add to palette" option, I can add a checkbox or a separate menu item.

Future behavior:
- I will append a similar entry to this file for each substantive code or UI change I make from now on, including the date, changed files, short summary, and why the change was made. If you prefer automation via a script or git hook, I can add that instead — tell me which metadata to include.

---

### Date: 2026-05-24
Author: GitHub Copilot (assistant)
Files changed:
- UI/NoteWindow.xaml

Change summary:
- Added a `Crystal clear` toggle to the note context menu (IsCheckable). It binds two-way to `NoteViewModel.IsCrystalClear` so users can enable crystal-clear mode per-note.

Reason:
- The user asked where the crystal clear option is; this adds an explicit, discoverable toggle in the note options so users can enable/disable the mode.

Notes / Testing:
- Build verified. The toggle updates the note's appearance immediately (transparent background + white text).

---

### Date: 2026-05-24
Author: GitHub Copilot (assistant)
Files changed:
- UI/MainDashboardWindow.xaml

Change summary:
- Removed the `System` theme icon button from the main dashboard title bar. The `System` option remains available in the system tray menu (unchanged).

Reason:
- The user asked to remove the system theme icon from the main body window while keeping it in the tray menu for convenience.

Notes / Testing:
- Build verified. Theme selection still works via the remaining Light/Dark buttons in the dashboard; System selection remains available from the tray menu.
