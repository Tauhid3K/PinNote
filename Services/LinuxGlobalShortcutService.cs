using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace PinNote.Services
{
    public class LinuxGlobalShortcutService : IGlobalShortcutService
    {
        private Action? _onTriggered;
        private Thread? _x11Thread;
        private bool _isRunning;
        private IntPtr _display = IntPtr.Zero;

        public bool IsWayland =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
            string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

        public bool RegisterQuickNoteShortcut(Action onTriggered)
        {
            if (!OperatingSystem.IsLinux()) return false;

            _onTriggered = onTriggered;

            if (IsWayland)
            {
                Debug.WriteLine("LinuxGlobalShortcutService: Running under Wayland. Global keystroke grabbing is restricted by design.");
                Debug.WriteLine("LinuxGlobalShortcutService: Fallback enabled: bind 'pinnote --new-note' in your Desktop Environment keyboard settings.");
                return false;
            }

            try
            {
                _isRunning = true;
                _x11Thread = new Thread(RunX11Loop)
                {
                    IsBackground = true,
                    Name = "PinNote_X11_Hotkey"
                };
                _x11Thread.Start();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinuxGlobalShortcutService X11 error: {ex.Message}");
                return false;
            }
        }

        private void RunX11Loop()
        {
            try
            {
                if (!NativeLibrary.TryLoad("libX11.so.6", out var libX11))
                {
                    if (!NativeLibrary.TryLoad("libX11.so", out libX11))
                    {
                        Debug.WriteLine("LinuxGlobalShortcutService: libX11 not found.");
                        return;
                    }
                }

                var xOpenDisplay = Marshal.GetDelegateForFunctionPointer<XOpenDisplayDelegate>(NativeLibrary.GetExport(libX11, "XOpenDisplay"));
                var xDefaultRootWindow = Marshal.GetDelegateForFunctionPointer<XDefaultRootWindowDelegate>(NativeLibrary.GetExport(libX11, "XDefaultRootWindow"));
                var xStringToKeysym = Marshal.GetDelegateForFunctionPointer<XStringToKeysymDelegate>(NativeLibrary.GetExport(libX11, "XStringToKeysym"));
                var xKeysymToKeycode = Marshal.GetDelegateForFunctionPointer<XKeysymToKeycodeDelegate>(NativeLibrary.GetExport(libX11, "XKeysymToKeycode"));
                var xGrabKey = Marshal.GetDelegateForFunctionPointer<XGrabKeyDelegate>(NativeLibrary.GetExport(libX11, "XGrabKey"));
                var xNextEvent = Marshal.GetDelegateForFunctionPointer<XNextEventDelegate>(NativeLibrary.GetExport(libX11, "XNextEvent"));
                var xCloseDisplay = Marshal.GetDelegateForFunctionPointer<XCloseDisplayDelegate>(NativeLibrary.GetExport(libX11, "XCloseDisplay"));

                _display = xOpenDisplay(IntPtr.Zero);
                if (_display == IntPtr.Zero)
                {
                    Debug.WriteLine("LinuxGlobalShortcutService: Failed to open X display.");
                    return;
                }

                IntPtr root = xDefaultRootWindow(_display);
                IntPtr keysym = xStringToKeysym("n");
                byte keycode = xKeysymToKeycode(_display, keysym);

                const uint ControlMask = (1 << 2);
                const uint Mod1Mask = (1 << 3); // Alt
                uint modifiers = ControlMask | Mod1Mask;

                // Grab key on root window (modifiers: Ctrl + Alt)
                xGrabKey(_display, keycode, modifiers, root, 1, 1, 1);
                // Also grab with NumLock / CapsLock variants to prevent lock-key suppression
                xGrabKey(_display, keycode, modifiers | 0x10 /* Mod2/NumLock */, root, 1, 1, 1);

                byte[] xEvent = new byte[192];
                while (_isRunning)
                {
                    xNextEvent(_display, xEvent);
                    int eventType = BitConverter.ToInt32(xEvent, 0);
                    const int KeyPress = 2;

                    if (eventType == KeyPress)
                    {
                        try
                        {
                            _onTriggered?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"LinuxGlobalShortcutService callback error: {ex}");
                        }
                    }
                }

                xCloseDisplay(_display);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinuxGlobalShortcutService loop terminated: {ex.Message}");
            }
        }

        public void Unregister()
        {
            _isRunning = false;
        }

        public void Dispose()
        {
            Unregister();
        }

        private delegate IntPtr XOpenDisplayDelegate(IntPtr displayName);
        private delegate IntPtr XDefaultRootWindowDelegate(IntPtr display);
        private delegate IntPtr XStringToKeysymDelegate(string str);
        private delegate byte XKeysymToKeycodeDelegate(IntPtr display, IntPtr keysym);
        private delegate int XGrabKeyDelegate(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow, int ownerEvents, int pointerMode, int keyboardMode);
        private delegate int XNextEventDelegate(IntPtr display, byte[] eventReturn);
        private delegate int XCloseDisplayDelegate(IntPtr display);
    }
}
