// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class PtyTests
    {
        private static readonly int TestTimeoutMs = Debugger.IsAttached ? 1000_000_000 : 5_000;

        private CancellationToken TimeoutToken { get; } = new CancellationTokenSource(TestTimeoutMs).Token;

        [Fact]
        public async Task ConnectToTerminal()
        {
            const uint CtrlCExitCode = 0xC000013A;

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            const string Data = "abc✓ЖЖЖ①Ⅻㄨㄩ 啊阿鼾齄丂丄狚狛狜狝﨨﨩ˊˋ˙– ⿻〇㐀㐁䶴䶵";

            string app = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : "sh";
            var options = new PtyOptions
            {
                Name = "Custom terminal",
                Cols = Data.Length + Environment.CurrentDirectory.Length + 50,
                Rows = 25,
                Cwd = Environment.CurrentDirectory,
                App = app,
                Environment = new Dictionary<string, string>()
                {
                    { "FOO", "bar" },
                    { "Bazz", null },
                },
            };

            IPtyConnection terminal = await PtyProvider.SpawnAsync(options, null, this.TimeoutToken);

            var processExitedTcs = new TaskCompletionSource<uint>();
            terminal.ProcessExited += (sender, e) => processExitedTcs.TrySetResult((uint)terminal.ExitCode);

            string GetTerminalExitCode() =>
                processExitedTcs.Task.IsCompleted ? $". Terminal process has exited with exit code {processExitedTcs.Task.GetAwaiter().GetResult()}." : string.Empty;

            var firstOutput = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstDataFound = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var output = string.Empty;
            var checkTerminalOutputAsync = Task.Run(async () =>
            {
                var buffer = new byte[4096];
                var ansiRegex = new Regex(
                    @"[\u001B\u009B][[\]()#;?]*(?:(?:(?:[a-zA-Z\d]*(?:;[a-zA-Z\d]*)*)?\u0007)|(?:(?:\d{1,4}(?:;\d{0,4})*)?[\dA-PRZcf-ntqry=><~]))");

                while (!this.TimeoutToken.IsCancellationRequested && !processExitedTcs.Task.IsCompleted)
                {
                    int count = await terminal.ReaderStream.ReadAsync(buffer, 0, buffer.Length, this.TimeoutToken);
                    if (count == 0)
                    {
                        break;
                    }

                    firstOutput.TrySetResult(null);

                    output += encoding.GetString(buffer, 0, count);
                    output = output.Replace("\r", string.Empty).Replace("\n", string.Empty);
                    output = ansiRegex.Replace(output, string.Empty);

                    var index = output.IndexOf(Data);
                    if (index >= 0)
                    {
                        firstDataFound.TrySetResult(null);
                        if (index <= output.Length - (2 * Data.Length)
                            && output.IndexOf(Data, index + Data.Length) >= 0)
                        {
                            return true;
                        }
                    }
                }

                firstOutput.TrySetCanceled();
                firstDataFound.TrySetCanceled();
                return false;
            });

            try
            {
                await firstOutput.Task;
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    $"Could not get any output from terminal{GetTerminalExitCode()}",
                    exception);
            }

            try
            {
                byte[] commandBuffer = encoding.GetBytes("echo " + Data);
                await terminal.WriterStream.WriteAsync(commandBuffer, 0, commandBuffer.Length, this.TimeoutToken);

                await firstDataFound.Task;

                await terminal.WriterStream.WriteAsync(new byte[] { 0x0D }, 0, 1, this.TimeoutToken); // Enter

                Assert.True(await checkTerminalOutputAsync);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Could not get expected data from terminal.{GetTerminalExitCode()} Actual terminal output:\n{output}",
                    exception);
            }

            terminal.Resize(40, 10);

            terminal.Dispose();

            using (this.TimeoutToken.Register(() => processExitedTcs.TrySetCanceled(this.TimeoutToken)))
            {
                uint exitCode = await processExitedTcs.Task;
                Assert.True(
                    exitCode == CtrlCExitCode || // WinPty terminal exit code.
                    exitCode == 1); // Pseudo Console exit code on Win 10.
            }

            Assert.True(terminal.WaitForExit(TestTimeoutMs));
        }
    }
}
