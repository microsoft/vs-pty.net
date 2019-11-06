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
        [Fact(Skip = "Diagnosing issues on mac/linux")]
        public async Task SuccessfulExitTest()
        {
            var completionSource = new TaskCompletionSource<int>();

            using IPtyConnection terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);
            terminal.ProcessExited += (sender, e) =>
            {
                completionSource.SetResult(e.ExitCode);
            };

            await terminal.RunCommand("exit", Utilities.TimeoutToken);

            var exitCode = await completionSource.Task.WithCancellation(Utilities.TimeoutToken);

            Assert.Equal(0, exitCode);
            Assert.Equal(0, terminal.ExitCode);
        }

        [Fact(Skip = "Diagnosing issues on mac/linux")]
        public async Task UnsuccessfulExitTest()
        {
            var completionSource = new TaskCompletionSource<int>();

            using IPtyConnection terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);
            terminal.ProcessExited += (sender, e) =>
            {
                completionSource.SetResult(e.ExitCode);
            };

            await terminal.RunCommand("exit 1", Utilities.TimeoutToken);

            var exitCode = await completionSource.Task.WithCancellation(Utilities.TimeoutToken);

            Assert.Equal(1, exitCode);
            Assert.Equal(1, terminal.ExitCode);
        }

        [Fact(Skip = "Diagnosing issues on mac/linux")]
        public async Task ForceKillTest()
        {
            var completionSource = new TaskCompletionSource<int>();

            using IPtyConnection terminal = await Utilities.CreateConnectionAsync(Utilities.TimeoutToken);
            terminal.ProcessExited += (sender, e) =>
            {
                completionSource.SetResult(e.ExitCode);
            };

            terminal.Kill();

            await completionSource.Task.WithCancellation(Utilities.TimeoutToken);
        }
    }
}
