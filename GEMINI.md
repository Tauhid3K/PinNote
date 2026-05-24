# PinNote Project Instructions

## Project Overview
PinNote is a WPF-based sticky note application for Windows.

## Development Workflow


## [Show All Notes Restore Fix] - May 25, 2026

### Changes
- **Show All Notes Behavior**: Updated the show-all-notes flow to reopen every saved note window, including notes that are saved but not currently visible.
	- *Rationale*: The user wanted the action to restore the remaining saved notes, not just the dashboard summary or already open windows.
- **Docs Sync**: Updated `README.md` and the repository change logs to match the new behavior.
	- *Rationale*: Documentation should reflect the actual restore behavior and installer flow.

### Verification
- `dotnet build` completed successfully; any warning was due to the app already running and locking the output binary.

- Follow MVVM strictly.
- Keep services decoupled and focused on single responsibilities.
- Use `Newtonsoft.Json` for data persistence.
