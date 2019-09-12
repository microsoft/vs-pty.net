// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides the ability to spawn new processes under a pseudoterminal.
    /// </summary>
    public static class PtyProvider
    {
        private static readonly TraceSource Trace = new TraceSource(nameof(PtyProvider));

        private static readonly Lazy<IPtyProvider> WindowsProviderLazy = new Lazy<IPtyProvider>(() => new Windows.PtyProvider());

        private static readonly Lazy<IPtyProvider> LinuxProviderLazy = new Lazy<IPtyProvider>(() => new Linux.PtyProvider());

        private static readonly Lazy<IPtyProvider> MacProviderLazy = new Lazy<IPtyProvider>(() => new Mac.PtyProvider());

        private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        private static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static string PlatformName => RuntimeInformation.OSDescription;

        /// <summary>
        /// Spawn a new process connected to a pseudoterminal.
        /// </summary>
        /// <param name="options">The set of options for creating the pseudoterminal.</param>
        /// <param name="environment">The environment variables that the spawned process will have.</param>
        /// <param name="cancellationToken">The token to cancel process creation early.</param>
        /// <returns>A <see cref="Task{IPtyConnection}"/> that completes once the process has spawned.</returns>
        public static Task<IPtyConnection> SpawnAsync(
            PtyOptions options,
            IDictionary<string, string> environment,
            CancellationToken cancellationToken)
        {
            if (IsWindows)
            {
                return WindowsProviderLazy.Value.StartTerminalAsync(options, environment, Trace, cancellationToken);
            }
            else if (IsLinux)
            {
                return LinuxProviderLazy.Value.StartTerminalAsync(options, environment, Trace, cancellationToken);
            }
            else if (IsMac)
            {
                return MacProviderLazy.Value.StartTerminalAsync(options, environment, Trace, cancellationToken);
            }

            throw new PlatformNotSupportedException();
        }
    }
}
