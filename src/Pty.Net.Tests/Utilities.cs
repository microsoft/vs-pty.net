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
        public static readonly int TestTimeoutMs = Debugger.IsAttached ? 300_000 : 5_000;

        public static CancellationToken TimeoutToken { get; } = new CancellationTokenSource(TestTimeoutMs).Token;

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

        private static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
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
