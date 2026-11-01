using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace PinNote.Services
{
    public class WindowsGlobalShortcutService : IGlobalShortcutService
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9001;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_N = 0x4E;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        private Thread? _messageThread;
        private uint _messageThreadId;
        private Action? _onTriggered;
        private bool _isRegistered;
        private readonly ManualResetEventSlim _initEvent = new(false);

        public bool RegisterQuickNoteShortcut(Action onTriggered)
        {
            if (!OperatingSystem.IsWindows()) return false;

            _onTriggered = onTriggered;

            _messageThread = new Thread(RunMessagePump)
            {
                IsBackground = true,
                Name = "PinNote_Hotkey_Thread"
            };
            _messageThread.SetApartmentState(ApartmentState.STA);
            _messageThread.Start();

            _initEvent.Wait(2000);
            return _isRegistered;
        }

        private void RunMessagePump()
        {
            _messageThreadId = GetCurrentThreadId();

            // Register Ctrl+Alt+N on thread message queue (hWnd = IntPtr.Zero)
            uint modifiers = MOD_CONTROL | MOD_ALT | MOD_NOREPEAT;
            _isRegistered = RegisterHotKey(IntPtr.Zero, HOTKEY_ID, modifiers, VK_N);

            if (!_isRegistered)
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"WindowsGlobalShortcutService: RegisterHotKey failed with error code {error}.");
            }
            else
            {
                Debug.WriteLine("WindowsGlobalShortcutService: Ctrl+Alt+N successfully registered.");
            }

            _initEvent.Set();

            if (!_isRegistered) return;

            while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_HOTKEY && (int)msg.wParam == HOTKEY_ID)
                {
                    try
                    {
                        _onTriggered?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error executing hotkey callback: {ex}");
                    }
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
            _isRegistered = false;
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        public void Unregister()
        {
            if (_isRegistered && _messageThreadId != 0)
            {
                const uint WM_QUIT = 0x0012;
                PostThreadMessage(_messageThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                _isRegistered = false;
            }
        }

        public void Dispose()
        {
            Unregister();
            _initEvent.Dispose();
        }
    }
}
