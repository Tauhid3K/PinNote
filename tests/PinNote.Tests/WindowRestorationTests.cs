using System.Collections.Generic;
using PinNote.Services;
using Xunit;

namespace PinNote.Tests
{
    public class WindowRestorationTests
    {
        [Fact]
        public void SanitizeBounds_ClampsSmallDimensionsToMinimums()
        {
            var screens = new List<ScreenBounds> { new(0, 0, 1920, 1080) };

            var bounds = WindowRestorationHelper.SanitizeBounds(100, 100, 50, 40, screens);

            Assert.Equal(WindowRestorationHelper.DefaultWidth, bounds.Width);
            Assert.Equal(WindowRestorationHelper.DefaultHeight, bounds.Height);
        }

        [Fact]
        public void SanitizeBounds_PreservesNegativeCoordinatesOnSecondaryMonitor()
        {
            // Primary monitor at (0, 0), secondary monitor to the left at (-1920, 0)
            var screens = new List<ScreenBounds>
            {
                new(-1920, 0, 1920, 1080),
                new(0, 0, 1920, 1080)
            };

            // Note placed on the secondary monitor
            var bounds = WindowRestorationHelper.SanitizeBounds(-1500, 200, 300, 300, screens);

            Assert.Equal(-1500, bounds.X);
            Assert.Equal(200, bounds.Y);
            Assert.Equal(300, bounds.Width);
            Assert.Equal(300, bounds.Height);
        }

        [Fact]
        public void SanitizeBounds_WhenMonitorDisconnected_RelocatesToPrimaryScreen()
        {
            // Note was previously placed at (-1500, 200), but secondary monitor is gone now
            var screens = new List<ScreenBounds>
            {
                new(0, 0, 1920, 1080)
            };

            var bounds = WindowRestorationHelper.SanitizeBounds(-1500, 200, 300, 300, screens);

            // Should be relocated onto the primary screen (X >= 0, Y >= 0)
            Assert.True(bounds.X >= 0);
            Assert.True(bounds.Y >= 0);
            Assert.True(bounds.X + bounds.Width <= 1920);
            Assert.True(bounds.Y + bounds.Height <= 1080);
        }

        [Fact]
        public void SanitizeBounds_WhenPushedOffBottomRight_ClampsWithinScreen()
        {
            var screens = new List<ScreenBounds> { new(0, 0, 1920, 1080) };

            // Note placed at (1900, 1060), almost off-screen
            var bounds = WindowRestorationHelper.SanitizeBounds(1900, 1060, 250, 250, screens);

            // Title bar and note must be kept visible
            Assert.True(bounds.X + 60 <= 1920);
            Assert.True(bounds.Y + 30 <= 1080);
        }

        [Fact]
        public void SanitizeBounds_PreservesNegativeYOnMonitorAbovePrimary()
        {
            // Primary monitor at (0, 0), secondary monitor above at (0, -1080)
            var screens = new List<ScreenBounds>
            {
                new(0, -1080, 1920, 1080),
                new(0, 0, 1920, 1080)
            };

            var bounds = WindowRestorationHelper.SanitizeBounds(100, -800, 300, 300, screens);

            Assert.Equal(100, bounds.X);
            Assert.Equal(-800, bounds.Y);
            Assert.Equal(300, bounds.Width);
            Assert.Equal(300, bounds.Height);
        }

        [Fact]
        public void SanitizeBounds_ClampsOversizedWindowToDisplaySize()
        {
            var screens = new List<ScreenBounds> { new(0, 0, 1920, 1080) };

            var bounds = WindowRestorationHelper.SanitizeBounds(100, 100, 3000, 2000, screens);

            Assert.True(bounds.Width <= 1920);
            Assert.True(bounds.Height <= 1080);
        }
    }
}
