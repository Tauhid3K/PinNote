using System;
using Avalonia;

namespace PinNote
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
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
