# PinNote Codex Work Steps Log

## Purpose

This file tracks every meaningful change Codex makes to the project, what was changed, and why it was changed.
Future edits should add a new entry here so the project keeps a clear history of UI, UX, code, and behavior decisions.

The project also has `GEMINI.md` for Gemini-facing instructions and `DEVELOPMENT_LOG.md` for the general development log.

## 2026-05-23 - UI/UX Polish Pass

1. Reviewed the project structure and source files.
   Files checked included `App.xaml`, `App.xaml.cs`, the UI windows, view models, models, and services.
   Why: To understand the current app flow before changing anything, especially because PinNote is a tray-first WPF sticky notes app.

2. Identified generated files and ignored them for UI analysis.
   Generated folders included `bin/` and `obj/`.
   Why: These files are build outputs and should not drive design or code decisions.

3. Found broken text encoding in dashboard buttons.
   The dashboard had corrupted characters such as mojibake in the minimize, maximize, and delete controls.
   Why: Broken glyphs make the app look unfinished and unprofessional.

4. Rebuilt `MainDashboardWindow.xaml` with cleaner dashboard UI.
   Updated the titlebar buttons to use Segoe MDL2 Assets icon glyphs.
   Added reusable styles for titlebar and primary buttons.
   Why: This gives the dashboard a more polished Windows-style interface and removes broken characters.

5. Added a dashboard empty state.
   Added a centered "No notes found" message and a direct "+ New Note" button.
   Why: An empty dashboard previously looked like a broken or blank app. A clear empty state guides the user.

6. Added `CollectionEmptyToVisibilityConverter.cs`.
   This converter shows or hides dashboard content based on whether the filtered notes collection is empty.
   Why: It keeps the XAML simple and makes the empty state reactive to search results.

7. Improved dashboard note cards.
   Cards are now larger, have a clearer title area, preview text, a visible delete icon, hover lift, and stronger shadow feedback.
   Why: Notes should feel clickable, scannable, and more professional in the all-notes dashboard.

8. Changed dashboard cards from double-click open to single-click open.
   Updated `NoteCard_MouseLeftButtonDown` in `MainDashboardWindow.xaml.cs`.
   Why: Single-click is easier and more discoverable for a card-based dashboard, especially with the "Click to open" cue.

9. Improved search placeholder text.
   Changed the placeholder from "Search" to "Search notes".
   Why: The action is clearer and more specific.

10. Made note close behavior safer.
    The note titlebar close button now calls `CloseNote_Click` instead of `DeleteCommand`.
    Why: Users expect close to hide or close a window, not permanently delete the note.

11. Added `CloseNote_Click` in `NoteWindow.xaml.cs`.
    It saves pending editor content and closes the note window.
    Why: This preserves user text before closing while avoiding accidental deletion.

12. Added a closing handler for `NoteWindow`.
    `NoteWindow_Closing` saves pending editor content before the window closes.
    Why: Any close path should preserve the latest typed content.

13. Replaced rough note toolbar labels with cleaner icon glyphs.
    Replaced "..." with an options icon, "*" with a list icon, "Img" with an image icon, and the close "x" with a Windows close glyph.
    Why: Icon buttons are easier to scan and look more consistent with a desktop app.

14. Added debounced editor saving.
    Added a `DispatcherTimer` in `NoteWindow.xaml.cs` that saves after typing pauses for 450 ms.
    Why: The app was saving rich text on every keystroke, which can cause unnecessary disk writes and make typing feel less smooth.

15. Updated `SaveEditorContent` to stop the pending save timer before writing.
    Why: This avoids duplicate saves when a manual save happens while a debounced save is pending.

16. Improved rich-text previews in the dashboard.
    `StripTagsConverter` now tries to load WPF XAML into a `FlowDocument` and reads plain text from `TextRange`.
    Why: Regex stripping rich-text XAML can produce messy previews. Parsing the document gives cleaner note previews.

17. Kept a plain fallback preview path.
    If rich-text parsing fails, the converter still strips markup and normalizes whitespace.
    Why: Older or unusual saved content should still produce a readable preview instead of failing.

18. Built the project with `dotnet build`.
    Result: Build succeeded.
    Why: To verify the XAML and C# changes compile correctly.

19. Noted existing NuGet warning.
    Warning: `H.NotifyIcon.Wpf 2.0.17` depends on `H.NotifyIcon >= 2.0.17`, but `H.NotifyIcon 2.0.24` was resolved.
    Why: The build works, but the package version mismatch should be documented for future cleanup.

## 2026-05-23 - Renamed Codex Work Log

1. Created `CODEX.md` and moved the Codex work steps into it.
   Why: The project already has `GEMINI.md`, so `CODEX.md` is the matching convention for Codex-specific notes and change history.

2. Removed the temporary `WORK_STEPS.txt` file.
   Why: Keeping both files would split the history and make future updates confusing.

## 2026-05-23 - System Tray Minimize Fix

1. Replaced the XAML-owned `H.NotifyIcon` tray icon with a code-owned Windows Forms `NotifyIcon`.
   Files changed: `App.xaml`, `App.xaml.cs`, and `PinNote.csproj`.
   Why: The dashboard was hiding on minimize but the app was not reliably appearing in the Windows system tray. A `System.Windows.Forms.NotifyIcon` uses the direct Windows tray integration path and is more dependable for this app.

2. Enabled Windows Forms support in the WPF project.
   File changed: `PinNote.csproj`.
   Why: The project now uses `System.Windows.Forms.NotifyIcon` for tray behavior.

3. Disabled SDK implicit usings.
   File changed: `PinNote.csproj`.
   Why: Enabling Windows Forms introduced global namespace collisions with WPF types such as `Application`, `Brush`, `Color`, `Image`, and `DataFormats`. The source files already declare their namespaces explicitly, so disabling implicit usings keeps WPF and WinForms types from clashing.

4. Added direct tray icon behavior.
   File changed: `App.xaml.cs`.
   The tray icon now supports left-click to show the dashboard, right-click menu actions for new note/show/hide/bring-on-top/exit, balloon notifications, and explicit cleanup on app exit.
   Why: Users need a reliable way to restore the app after minimizing it.

5. Changed dashboard minimize and close behavior to hide to tray.
   File changed: `MainDashboardWindow.xaml.cs`.
   Why: Closing the dashboard could leave the app running but harder to restore. Minimize and close now both keep PinNote available from the tray.

6. Removed the unused `H.NotifyIcon.Wpf` package reference.
   File changed: `PinNote.csproj`.
   Why: The app no longer uses that package, and removing it also clears the NuGet warning about `H.NotifyIcon` resolving to a different version.

7. Removed the unused WPF `TrayMenu` resource from `App.xaml`.
   Why: The tray menu is now built in `App.xaml.cs` for the Windows Forms tray icon, so keeping the old XAML menu would be misleading.

8. Built the project with `dotnet build`.
   Result: Build succeeded with 0 warnings and 0 errors.
   Why: To verify the tray fix compiles cleanly after the WPF/WinForms integration change.

## 2026-05-23 - Note Window Wireframe Layout

1. Rebuilt `NoteWindow.xaml` to match the requested wireframe.
   The note now has an always-visible top options row, a separate editable title row, a large note editor area, a bottom formatting toolbar, and a bottom-right resize handle.
   Why: The requested GUI is simpler, more direct, and keeps key actions visible instead of hiding controls until the note is active.

2. Made the option row always visible.
   It includes new note, pin, settings, and close controls.
   Why: The user wanted options like plus, pin, settings, and cross visible at the top.

3. Separated title editing from the top options row.
   The title row uses `TitleFontSize`, while the note body uses `BodyFontSize`.
   Why: The title should change size based on the text-size setting without being crowded by the option buttons.

4. Kept the bottom formatting toolbar visible.
   The toolbar now shows `B`, `I`, `U`, `*`, and `img` like the requested sketch.
   Why: The formatting actions should be easy to find and match the provided design.

5. Changed new-note default colors.
   File changed: `NoteModel.cs`.
   New notes now default to a sky-blue header (`#FFD1EAF7`) and a white body (`#FFFFFFFF`) with full opacity.
   Why: The user requested the default color to be white or sky blue.

6. Updated color behavior to support separate title/body colors.
   File changed: `NoteViewModel.cs`.
   Changing `BodyColor` no longer forces `TitleBarColor` to the same value.
   Why: The new default uses a sky-blue top area with a white writing area, so the model needs to support two-tone notes.

7. Added a color palette option for the new default look.
   File changed: `NoteWindow.xaml.cs`.
   The palette now includes "Sky Header / White Note".
   Why: Users should be able to return a customized note to the new default style.

8. Improved titlebar dragging for the new top row.
   File changed: `NoteWindow.xaml.cs`.
   The top row can drag the note while button clicks still work normally.
   Why: The wireframe includes a move area, so dragging should feel natural.

9. Built the project with `dotnet build`.
   Result: Build succeeded with 0 warnings and 0 errors.
   Why: To verify the new XAML and default color changes compile correctly.

## 2026-05-24 - Tray Fallback and Idle Note Chrome

1. Changed `App.EnsureTrayVisible` to return a success flag.
   Files changed: `App.xaml.cs` and `MainDashboardWindow.xaml.cs`.
   Why: If the system tray icon cannot be created or confirmed, the app needs a fallback instead of hiding the dashboard with no visible way back.

2. Added a taskbar fallback for minimize.
   File changed: `MainDashboardWindow.xaml.cs`.
   If tray setup succeeds, the dashboard hides to tray. If tray setup fails, the dashboard remains minimized in the taskbar.
   Why: The user requested that when system tray behavior does not work, the app should show in the taskbar.

3. Restored dashboard taskbar behavior when reopening from tray/taskbar.
   File changed: `MainViewModel.cs`.
   Why: A dashboard minimized to the taskbar should reopen normally and return to the tray-first hidden-taskbar behavior afterward.

4. Added idle hiding for note chrome.
   Files changed: `NoteWindow.xaml` and `NoteWindow.xaml.cs`.
   The top options row and bottom formatting toolbar now hide after two seconds of inactivity and reappear on mouse movement or keyboard focus.
   Why: The user wanted the options row and lower formatting row hidden in idle mode.

5. Kept the title row draggable while the options row is hidden.
   File changed: `NoteWindow.xaml`.
   Why: The note still needs an easy move area when idle chrome disappears.

6. Made the title/options/toolbar color a darker shade of the chosen note color.
   Files changed: `NoteWindow.xaml` and `NoteViewModel.cs`.
   Why: The user wanted the title bar to be a little darker than the note body for every selected color.

7. Polished the bottom formatting toolbar.
   File changed: `NoteWindow.xaml`.
   The toolbar now uses cleaner spacing, a separator, and icon glyphs for bullet list and image insertion.
   Why: The lower bar should feel more professional while still matching the requested `B`, `I`, `U`, list, and image controls.

8. Built the project with `dotnet build`.
   Result: Build succeeded with 0 warnings and 0 errors.
   Why: To verify the tray fallback, idle chrome behavior, and color changes compile correctly.

## 2026-05-24 - Note Toolbar Layout Refinement

1. Removed the three-line drag icon from the top note options row.
   File changed: `NoteWindow.xaml`.
   Why: The icon did not represent a user action clearly and the user asked why it was there.

2. Moved the formatting toolbar flush into the bottom bar.
   File changed: `NoteWindow.xaml`.
   Why: The previous layout had an extra lower strip for the resize handle, which made the format bar feel misplaced.

3. Changed the format toolbar from a horizontal stack to proportional grid columns.
   File changed: `NoteWindow.xaml`.
   Why: The buttons now keep a balanced position when the note window is resized.

4. Improved the resize handle.
   File changed: `NoteWindow.xaml`.
   The resize handle now appears as a visible bottom-right grip with a tooltip.
   Why: The previous resize square was too small and detached from the professional toolbar layout.

5. Changed the color palette layout to a fixed 6-column box grid.
   File changed: `NoteWindow.xaml`.
   Why: The user requested a box/grid palette instead of a long line-like layout.

6. Built the project into `verify-build` using `dotnet build -o .\verify-build`.
   Result: Build succeeded with 0 warnings and 0 errors.
   Why: The normal `bin\Debug` output was locked by the currently running PinNote process, so a separate output folder verified the code without closing the user's app.
