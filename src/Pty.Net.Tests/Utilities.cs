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

    internal static class Utilities
    {
        public static readonly int TestTimeoutMs = Debugger.IsAttached ? 300_000 : 10_000;

        public static CancellationToken TimeoutToken => new CancellationTokenSource(TestTimeoutMs).Token;

        public static async Task<IPtyConnection> CreateConnectionAsync(CancellationToken token = default)
        {
            string app = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : "sh";
            var options = new PtyOptions
            {
                Name = "Custom terminal",
                Cols = 80,
                Rows = 25,
                Cwd = Environment.CurrentDirectory,
                App = app,
                Environment = new Dictionary<string, string>()
                {
                    { "FOO", "bar" },
                    { "Bazz", string.Empty },
                },
            };

            return await PtyProvider.SpawnAsync(options, token);
        }

        public static async Task RunCommand(this IPtyConnection terminal, string command, CancellationToken token = default)
        {
            var processExitedTcs = new TaskCompletionSource<uint>();
            terminal.ProcessExited += (sender, e) => processExitedTcs.TrySetResult((uint)terminal.ExitCode);
            string GetTerminalExitCode() =>
                processExitedTcs.Task.IsCompleted ? $". Terminal process has exited with exit code {processExitedTcs.Task.GetAwaiter().GetResult()}." : string.Empty;

            var firstOutput = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstDataFound = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            var checkForFirstOutput = Task.Run(async () =>
            {
                var buffer = new byte[4096];

                var ansiRegex = new Regex(
                   @"[\u001B\u009B][[\]()#;?]*(?:(?:(?:[a-zA-Z\d]*(?:;[a-zA-Z\d]*)*)?\u0007)|(?:(?:\d{1,4}(?:;\d{0,4})*)?[\dA-PRZcf-ntqry=><~]))");
                var output = string.Empty;

                while (!token.IsCancellationRequested)
                {
                    int count = await terminal.ReaderStream.ReadAsync(buffer, 0, buffer.Length).WithCancellation(token);
                    if (count == 0)
                    {
                        break;
                    }

                    firstOutput.TrySetResult(null);

                    output += encoding.GetString(buffer, 0, count);
                    output = output.Replace("\r", string.Empty).Replace("\n", string.Empty);
                    output = ansiRegex.Replace(output, string.Empty);
                    var index = output.IndexOf(command);
                    if (index >= 0)
                    {
                        firstDataFound.TrySetResult(null);
                        return;
                    }
                }

                firstOutput.TrySetCanceled();
                firstDataFound.TrySetCanceled();
                return;
            });

            byte[] commandBuffer = encoding.GetBytes(command);

            try
            {
                await firstOutput.Task;
                Console.WriteLine("first output found");
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    $"Could not get any output from terminal{GetTerminalExitCode()}",
                    exception);
            }

            await terminal.WriterStream.WriteAsync(commandBuffer, 0, commandBuffer.Length, token);
            await terminal.WriterStream.FlushAsync();

            await firstDataFound.Task.WithCancellation(token);

            await terminal.WriterStream.WriteAsync(new byte[] { 0x0D }, 0, 1, token);
            await terminal.WriterStream.FlushAsync();

            await checkForFirstOutput;
        }

        public static async Task<bool> FindOutput(Stream terminalReadStream, string search, CancellationToken token = default)
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var buffer = new byte[4096];

            var ansiRegex = new Regex(
               @"[\u001B\u009B][[\]()#;?]*(?:(?:(?:[a-zA-Z\d]*(?:;[a-zA-Z\d]*)*)?\u0007)|(?:(?:\d{1,4}(?:;\d{0,4})*)?[\dA-PRZcf-ntqry=><~]))");
            var searchRegex = new Regex(search);
            var output = string.Empty;

            while (!token.IsCancellationRequested)
            {
                int count = await terminalReadStream.ReadAsync(buffer, 0, buffer.Length).WithCancellation(token);
                if (count == 0)
                {
                    break;
                }

                output += encoding.GetString(buffer, 0, count);
                output = output.Replace("\r", string.Empty).Replace("\n", string.Empty);
                output = ansiRegex.Replace(output, string.Empty);
                if (searchRegex.IsMatch(output))
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task<string> RunAndFind(IPtyConnection terminal, string command, string search, CancellationToken token = default)
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var processExitedTcs = new TaskCompletionSource<uint>();
            terminal.ProcessExited += (sender, e) => processExitedTcs.TrySetResult((uint)terminal.ExitCode);

            string GetTerminalExitCode() =>
                processExitedTcs.Task.IsCompleted ? $". Terminal process has exited with exit code {processExitedTcs.Task.GetAwaiter().GetResult()}." : string.Empty;

            var firstOutput = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstDataFound = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var output = string.Empty;
            var regexOffset = 0;

            var checkTerminalOutputAsync = Task.Run(async () =>
            {
                var buffer = new byte[4096];
                var ansiRegex = new Regex(
                    @"[\u001B\u009B][[\]()#;?]*(?:(?:(?:[a-zA-Z\d]*(?:;[a-zA-Z\d]*)*)?\u0007)|(?:(?:\d{1,4}(?:;\d{0,4})*)?[\dA-PRZcf-ntqry=><~]))");
                var searchRegex = new Regex(search);
                while (!token.IsCancellationRequested && !processExitedTcs.Task.IsCompleted)
                {
                    int count = await terminal.ReaderStream.ReadAsync(buffer, 0, buffer.Length).WithCancellation(token);
                    if (count == 0)
                    {
                        break;
                    }

                    firstOutput.TrySetResult(null);

                    output += encoding.GetString(buffer, 0, count);
                    output = output.Replace("\r", string.Empty).Replace("\n", string.Empty);
                    output = ansiRegex.Replace(output, string.Empty);

                    Console.WriteLine($"output: {output}");
                    var index = output.IndexOf(command);
                    if (index >= 0)
                    {
                        regexOffset = index + command.Length;
                        firstDataFound.TrySetResult(null);
                        if (index <= output.Length - (2 * search.Length)
                            && output.IndexOf(search, index + search.Length) >= 0)
                        {
                            return search;
                        }
                        else if (searchRegex.IsMatch(output, regexOffset))
                        {
                            return searchRegex.Match(output, regexOffset).ToString();
                        }
                    }
                }

                firstOutput.TrySetCanceled();
                firstDataFound.TrySetCanceled();
                return null;
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
                byte[] commandBuffer = encoding.GetBytes(command);
                await terminal.WriterStream.WriteAsync(commandBuffer, 0, commandBuffer.Length, token);
                await terminal.WriterStream.FlushAsync();

                await firstDataFound.Task;

                await terminal.WriterStream.WriteAsync(new byte[] { 0x0D }, 0, 1, token); // Enter
                await terminal.WriterStream.FlushAsync();

                return await checkTerminalOutputAsync;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Could not get expected data from terminal.{GetTerminalExitCode()} Actual terminal output:\n{output}",
                    exception);
            }
        }

        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            // Rethrow any fault/cancellation exception, even if we awaited above.
            // But if we skipped the above if branch, this will actually yield
            // on an incompleted task.
            return await task.ConfigureAwait(false);
        }
    }
}
