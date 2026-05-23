# PinNote

PinNote is a lightweight, intuitive sticky note application designed for Windows. Built with WPF and .NET 8.0, it provides a seamless way to capture ideas, manage tasks, and keep important information always within reach.

## 🚀 Features

- **Dynamic Sticky Notes:** Create, edit, and resize notes with ease.
- **Always on Top (Pinning):** Pin critical notes to keep them visible above all other windows.
- **Persistence:** Your notes are automatically saved and restored every time you launch the application.
- **Central Dashboard:** Manage all your notes from a single, searchable dashboard.
- **Search & Filter:** Quickly find specific notes using the real-time search functionality.
- **System Tray Integration:** Minimizes to the system tray for a clutter-free workspace while remaining quickly accessible.
- **Customizable Appearance:** Adjust note colors, font sizes, and opacity to suit your preference.
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
