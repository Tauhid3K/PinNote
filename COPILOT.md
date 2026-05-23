# Development Steps Log (Copilot)

This file records each substantive change made to the repository, why the change was made, and any relevant notes or instructions. It is intended to be updated whenever code or configuration is changed so the project's history is clear for maintainers.

Format for entries:
- Date: YYYY-MM-DD
- Author: (assistant or user)
- Files changed: (list of relative paths)
- Change summary: (short description)
- Reason: (why it was needed)
- Notes: (any follow-ups, testing, or manual steps)

---

## Entries

### Date: 2026-05-24
Author: GitHub Copilot (assistant)
Files changed:
- ViewModels/MainViewModel.cs
- Services/AppStateService.cs
- App.xaml.cs
- UI/MainDashboardWindow.xaml
- UI/NoteWindow.xaml
- Themes/Light.xaml (added)
- Themes/Dark.xaml (added)
- STEPS.md (migrated to COPILOT.md)

Change summary:
- Modified hide behavior so "Hide all notes" hides only individual note windows and keeps the main dashboard visible.
- Added application theme support (System / Light / Dark), persisted via `AppStateService` and applied at startup.
- Added a Theme submenu to the tray icon to choose System/Light/Dark and persist selection.
- Added in-dashboard Theme selector (ComboBox) bound to `MainViewModel.ThemeMode`.
- Created `Themes/Light.xaml` and `Themes/Dark.xaml` resource dictionaries with base brushes.
- Updated `MainDashboardWindow.xaml` to use dynamic theme brushes for background, title, text, search, and primary button colors.
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

```powershell
dotnet run --project "PinNote.csproj" -f net8.0-windows
```

Follow-ups (suggested):
- Apply theme brushes to `NoteWindow` chrome (title/toolbar) so note chrome follows Light/Dark themes as well — currently notes preserve per-note coloring and only dashboard is themed.
- Optionally add a small settings panel describing the "System" option and its behavior.

---

### Date: 2026-05-24
Author: GitHub Copilot (assistant)
Files changed:
- UI/MainDashboardWindow.xaml
- ViewModels/MainViewModel.cs

Change summary:
- Replaced the theme `ComboBox` with three compact icon buttons (System, Light, Dark) in the dashboard title bar. Buttons are bound to `MainViewModel.SetThemeCommand` and visually indicate the selected theme.

Reason:
- The user requested icons instead of an options dropdown for theme selection; icons provide faster access and a more compact UI.

Notes / Testing:
- The new buttons use `Segoe MDL2 Assets` glyphs and the existing `TitleBarButtonStyle`. Restart the app to see the change.

### Date: 2026-05-24
Author: GitHub Copilot (assistant)
Files changed:
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
