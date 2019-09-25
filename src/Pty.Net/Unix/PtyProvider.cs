// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Unix
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstract class that provides a pty connection for unix-like machines.
    /// </summary>
    internal abstract class PtyProvider : IPtyProvider
    {
        /// <inheritdoc/>
        public abstract Task<IPtyConnection> StartTerminalAsync(PtyOptions options, TraceSource trace, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the arguments to pass to execvp.
        /// </summary>
        /// <param name="options">The options for spawning the pty.</param>
        /// <returns>An array of arguments to pass to execvp.</returns>
        protected static string?[] GetExecvpArgs(PtyOptions options)
        {
            // execvp(2) args array must end with null. The first arg is the app itself.
            if (options.CommandLine.Length == 0)
            {
                return new[] { options.App, null };
            }

            var result = new string?[options.CommandLine.Length + 2];
            Array.Copy(options.CommandLine, 0, result, 1, options.CommandLine.Length);
            result[0] = options.App;
            return result;
        }
    }
}
