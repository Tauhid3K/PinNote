using System;
using System.Collections.Generic;
using System.Linq;

namespace PinNote.Services
{
    public struct ScreenBounds
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public ScreenBounds(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double Right => X + Width;
        public double Bottom => Y + Height;

        public bool Intersects(double winX, double winY, double winW, double winH, double minVisibleW = 60, double minVisibleH = 30)
        {
            double overlapLeft = Math.Max(X, winX);
            double overlapTop = Math.Max(Y, winY);
            double overlapRight = Math.Min(Right, winX + winW);
            double overlapBottom = Math.Min(Bottom, winY + winH);

            double overlapW = Math.Max(0, overlapRight - overlapLeft);
            double overlapH = Math.Max(0, overlapBottom - overlapTop);

            return overlapW >= minVisibleW && overlapH >= minVisibleH;
        }
    }

    public struct SanitizedBounds
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public SanitizedBounds(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public static class WindowRestorationHelper
    {
        public const double MinWidth = 160;
        public const double MinHeight = 120;
        public const double DefaultWidth = 250;
        public const double DefaultHeight = 250;

        public static SanitizedBounds SanitizeBounds(
            double requestedX,
            double requestedY,
            double requestedWidth,
            double requestedHeight,
            IReadOnlyList<ScreenBounds>? availableScreens)
        {
            // Fallback screen if none provided
            if (availableScreens == null || availableScreens.Count == 0)
            {
                availableScreens = new[] { new ScreenBounds(0, 0, 1920, 1080) };
            }

            // Primary screen is assumed to be the one at or closest to (0, 0)
            var primaryScreen = availableScreens.FirstOrDefault(s => Math.Abs(s.X) < 1 && Math.Abs(s.Y) < 1);
            if (primaryScreen.Width <= 0)
            {
                primaryScreen = availableScreens[0];
            }

            // 1. Sanitize dimensions
            double width = requestedWidth;
            if (width < MinWidth || double.IsNaN(width) || double.IsInfinity(width)) width = DefaultWidth;

            double height = requestedHeight;
            if (height < MinHeight || double.IsNaN(height) || double.IsInfinity(height)) height = DefaultHeight;

            // Clamp max dimensions to primary screen size
            if (width > primaryScreen.Width) width = primaryScreen.Width - 40;
            if (height > primaryScreen.Height) height = primaryScreen.Height - 40;

            // 2. Check if the requested location intersects any active monitor
            bool intersectsAnyScreen = false;
            ScreenBounds containingScreen = primaryScreen;

            foreach (var screen in availableScreens)
            {
                if (screen.Intersects(requestedX, requestedY, width, height, minVisibleW: 60, minVisibleH: 30))
                {
                    intersectsAnyScreen = true;
                    containingScreen = screen;
                    break;
                }
            }

            double x = requestedX;
            double y = requestedY;

            if (intersectsAnyScreen)
            {
                // Ensure at least the title bar is visible within the containing monitor
                // Must not be pushed completely off the right or bottom
                if (x + 60 > containingScreen.Right)
                {
                    x = containingScreen.Right - width;
                }
                if (x < containingScreen.X - (width - 60))
                {
                    x = containingScreen.X;
                }
                if (y < containingScreen.Y)
                {
                    y = containingScreen.Y;
                }
                if (y + 30 > containingScreen.Bottom)
                {
                    y = containingScreen.Bottom - height;
                }
            }
            else
            {
                // The monitor where the note was originally placed no longer exists (e.g. disconnected)
                // Relocate safely onto the primary screen with standard margin
                x = primaryScreen.X + 60;
                y = primaryScreen.Y + 60;
            }

            return new SanitizedBounds(x, y, width, height);
        }
    }
}
