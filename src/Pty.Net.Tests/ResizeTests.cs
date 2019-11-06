// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Tests
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
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

            Assert.Throws<OverflowException>(() => terminal.Resize(short.MaxValue + 1, 25));
            Assert.Throws<OverflowException>(() => terminal.Resize(80, short.MaxValue + 1));
            Assert.Throws<OverflowException>(() => terminal.Resize(short.MaxValue + 1, short.MaxValue + 1));
        }

        [Fact]
        public async Task LargeValidSizeTest()
        {
            using var terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);

            terminal.Resize(short.MaxValue, 25);
            terminal.Resize(80, short.MaxValue);
            terminal.Resize(short.MaxValue, short.MaxValue);
        }

        [Fact]
        public async Task ActuallyResizesTest()
        {
            using var terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);
            using var writer = new StreamWriter(terminal.WriterStream);

            terminal.Resize(72, 13);

            var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "mode\r"
                : "echo -n Lines: && tput lines && echo -n Columns: && tput cols\r";

            await writer.WriteAsync(command);
            await writer.FlushAsync();

            Assert.True(await Utilities.FindOutput(terminal.ReaderStream, "Lines:\\s*13\\s*Columns:\\s*72", Utilities.TimeoutToken));
        }
    }
}
