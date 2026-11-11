// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using System.Drawing;
using QRPeek.Core;
using Xunit;

namespace QRPeek.Tests
{
    public class ScreenPlaceTests
    {
        private static readonly Rectangle Main = new Rectangle(0, 0, 2560, 1440);

        [Fact]
        public void Names_screens_by_where_they_sit_next_to_the_main_one()
        {
            Assert.Equal(ScreenPlace.Main, ScreenCapture.PlaceOf(Main, Main));
            // A tablet extended to the left, smaller and lower than the main screen (the setup it was tried on).
            Assert.Equal(ScreenPlace.Left, ScreenCapture.PlaceOf(new Rectangle(-1920, 200, 1920, 1200), Main));
            Assert.Equal(ScreenPlace.Right, ScreenCapture.PlaceOf(new Rectangle(2560, -300, 1080, 1920), Main)); // portrait, offset up
            Assert.Equal(ScreenPlace.Above, ScreenCapture.PlaceOf(new Rectangle(320, -1080, 1920, 1080), Main));
            Assert.Equal(ScreenPlace.Below, ScreenCapture.PlaceOf(new Rectangle(0, 1440, 2560, 1440), Main));
        }

        [Fact]
        public void Tells_where_each_code_was_and_keeps_the_first_for_duplicates()
        {
            using (var main = new MonitorShot(Main, new Bitmap(1, 1), isPrimary: true))
            using (var left = new MonitorShot(new Rectangle(-1920, 0, 1920, 1080), new Bitmap(1, 1)))
            {
                Dictionary<string, ScreenPlace> places = ScreenCapture.PlacesOf(
                    new[] { left, main },
                    new IReadOnlyList<string>[] { new[] { "on-tablet", "both" }, new[] { "on-main", "both" } });

                Assert.Equal(3, places.Count);
                Assert.Equal(ScreenPlace.Left, places["on-tablet"]);
                Assert.Equal(ScreenPlace.Main, places["on-main"]);
                Assert.Equal(ScreenPlace.Left, places["both"]); // mirrored or spanning: the first screen wins
            }
        }

        [Theory]
        [InlineData("tr", (int)ScreenPlace.Left, "Soldaki ekranda")]
        [InlineData("tr", (int)ScreenPlace.Main, "Ana ekranda")]
        [InlineData("en", (int)ScreenPlace.Below, "On the lower screen")]
        public void Has_words_for_every_place(string lang, int place, string expected)
        {
            S.Init(lang);
            Assert.Equal(expected, S.OnScreen((ScreenPlace)place));
            S.Init("en");
        }
    }
}
