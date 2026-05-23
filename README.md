# PinNote

PinNote is a lightweight, intuitive sticky note application designed for Windows. Built with WPF and .NET 8.0, it provides a seamless way to capture ideas, manage tasks, and keep important information always within reach.

## 🚀 Features

- **Dynamic Sticky Notes:** Create, edit, and resize notes from any edge or corner.
- **Ultra-Small Responsive UI:** Shrink notes down to 100x100; toolbars automatically adapt and scroll to remain accessible.
- **Always on Top (Pinning):** Pin critical notes to keep them visible above all other windows.
- **Automatic Restoration:** Your notes are automatically restored to their exact screen positions every time you launch the application.
- **Resizable Dashboard:** A central, resizable dashboard to manage and search all your notes.
- **Professional Interface:** Clean, centered titles and a streamlined context menu with professional iconography.
- **System Tray Integration:** Runs silently in the tray with a subtle "notify only" minimize experience.
- **Customizable Appearance:** Adjust note colors, font sizes (including Extra Small), and opacity.
- **Auto-Startup:** Option to launch PinNote automatically when Windows starts.

## 🛠️ Tech Stack

- **Framework:** [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows Desktop)
- **UI Framework:** WPF (Windows Presentation Foundation)
- **Design Pattern:** MVVM (Model-View-ViewModel)
- **Key Libraries:**
  - [Newtonsoft.Json](https://www.newtonsoft.com/json): For robust data serialization and local storage.
  - [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon): For advanced system tray integration.

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
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or SDK installed on your machine.

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

## 📝 License
This project is licensed under the MIT License - see the LICENSE file for details.
