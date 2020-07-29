// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Tests
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Xunit;

    public class ResizeTests
    {
        [Fact]
        public async Task ZeroSizeTest()
        {
            using var terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);

            terminal.Resize(0, 0);
        }

        [Fact]
        public async Task NegativeSizeTest()
        {
            using var terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);

            Assert.Throws<ArgumentOutOfRangeException>(() => terminal.Resize(80, -25));
            Assert.Throws<ArgumentOutOfRangeException>(() => terminal.Resize(-80, 25));
            Assert.Throws<ArgumentOutOfRangeException>(() => terminal.Resize(-80, -25));
        }

        [Fact]
        public async Task LargeInvalidSizeTest()
        {
            using var terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);

            var size = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? short.MaxValue + 1
                : ushort.MaxValue + 1;

            Assert.Throws<OverflowException>(() => terminal.Resize(size, 25));
            Assert.Throws<OverflowException>(() => terminal.Resize(80, size));
            Assert.Throws<OverflowException>(() => terminal.Resize(size, size));
        }

        [Fact]
        public async Task LargeValidSizeTest()
        {
            using var terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);

            var size = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? (int)short.MaxValue
                : (int)ushort.MaxValue;

            terminal.Resize(size, 25);
            terminal.Resize(80, size);
            terminal.Resize(size, size);
        }

        [Fact]
        public async Task ActuallyResizesTest()
        {
            using var terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);
            terminal.Resize(72, 13);

            var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "mode"
                : "echo Lines: && tput lines && echo Columns: && tput cols";

            var output = await Utilities.RunAndFind(terminal, command, "Lines:\\D*(?<rows>\\d+).*Columns:\\D*(?<cols>\\d+)");

            var metches = Regex.Match(output, "Lines:\\D*(?<rows>\\d+).*Columns:\\D*(?<cols>\\d+)");

            Assert.Equal("13", metches.Groups["rows"].Value);
            Assert.Equal("72", metches.Groups["cols"].Value);
        }
    }
}
