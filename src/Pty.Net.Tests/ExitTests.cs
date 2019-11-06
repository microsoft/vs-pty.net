// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ExitTests
    {
        [Fact]
        public async Task SuccessfulExitTest()
        {
            var completionSource = new TaskCompletionSource<int>();

            Utilities.TimeoutToken.Register(() =>
            {
                completionSource.SetCanceled();
            });

            using IPtyConnection terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);
            terminal.ProcessExited += (sender, e) =>
            {
                completionSource.SetResult(e.ExitCode);
            };

            using var writer = new StreamWriter(terminal.WriterStream);
            using var reader = new StreamReader(terminal.ReaderStream);

            await writer.WriteAsync("exit 0\r");
            await writer.FlushAsync();

            var exitCode = await completionSource.Task;

            Assert.Equal(0, exitCode);
            Assert.Equal(0, terminal.ExitCode);
        }

        [Fact]
        public async Task UnsuccessfulExitTest()
        {
            var completionSource = new TaskCompletionSource<int>();

            Utilities.TimeoutToken.Register(() =>
            {
                completionSource.SetCanceled();
            });

            using IPtyConnection terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);
            terminal.ProcessExited += (sender, e) =>
            {
                completionSource.SetResult(e.ExitCode);
            };

            using var writer = new StreamWriter(terminal.WriterStream);
            using var reader = new StreamReader(terminal.ReaderStream);

            await writer.WriteAsync("exit 1\r");
            await writer.FlushAsync();

            var exitCode = await completionSource.Task;

            Assert.Equal(1, exitCode);
            Assert.Equal(1, terminal.ExitCode);
        }

        [Fact]
        public async Task ForceKillTest()
        {
            var completionSource = new TaskCompletionSource<int>();

            Utilities.TimeoutToken.Register(() =>
            {
                completionSource.SetCanceled();
            });

            using IPtyConnection terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);
            terminal.ProcessExited += (sender, e) =>
            {
                completionSource.SetResult(e.ExitCode);
            };

            terminal.Kill();

            await completionSource.Task;
        }
    }
}
