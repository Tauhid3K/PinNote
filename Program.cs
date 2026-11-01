using System;
using Avalonia;
using PinNote.Services;

namespace PinNote
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (Array.Exists(args, a => string.Equals(a, "--new-note", StringComparison.OrdinalIgnoreCase)))
            {
                if (SingleInstanceService.SendCommandToRunningInstanceAsync("NEW_NOTE").GetAwaiter().GetResult())
                {
                    return;
                }
            }
            else if (Array.Exists(args, a => string.Equals(a, "--show-dashboard", StringComparison.OrdinalIgnoreCase)))
            {
                if (SingleInstanceService.SendCommandToRunningInstanceAsync("SHOW_DASHBOARD").GetAwaiter().GetResult())
                {
                    return;
                }
            }
            else if (Array.Exists(args, a => string.Equals(a, "--show-all", StringComparison.OrdinalIgnoreCase)))
            {
                if (SingleInstanceService.SendCommandToRunningInstanceAsync("SHOW_ALL").GetAwaiter().GetResult())
                {
                    return;
                }
            }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<AvaloniaApp>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .With(new X11PlatformOptions
                {
                    RenderingMode = new[] { X11RenderingMode.Glx, X11RenderingMode.Egl, X11RenderingMode.Software }
                });
    }
}
