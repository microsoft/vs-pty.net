// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides platform specific functionality.
    /// </summary>
    internal static class PlatformServices
    {
        private static readonly Lazy<IPtyProvider> WindowsProviderLazy = new Lazy<IPtyProvider>(() => new Windows.PtyProvider());
        private static readonly Lazy<IPtyProvider> LinuxProviderLazy = new Lazy<IPtyProvider>(() => new Linux.PtyProvider());
        private static readonly Lazy<IPtyProvider> MacProviderLazy = new Lazy<IPtyProvider>(() => new Mac.PtyProvider());
        private static readonly Lazy<IPtyProvider> PtyProviderLazy;
        private static readonly IDictionary<string, string> WindowsPtyEnvironment = new Dictionary<string, string>();
        private static readonly IDictionary<string, string> UnixPtyEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "TERM", "xterm-256color" },

                // Make sure we didn't start our server from inside tmux.
            { "TMUX", string.Empty },
            { "TMUX_PANE", string.Empty },

                // Make sure we didn't start our server from inside screen.
                // http://web.mit.edu/gnu/doc/html/screen_20.html
            { "STY", string.Empty },
            { "WINDOW", string.Empty },

                // These variables that might confuse our terminal
            { "WINDOWID", string.Empty },
            { "TERMCAP", string.Empty },
            { "COLUMNS", string.Empty },
            { "LINES", string.Empty },
        };

        static PlatformServices()
        {
            if (IsWindows)
            {
                PtyProviderLazy = WindowsProviderLazy;
                EnvironmentVariableComparer = StringComparer.OrdinalIgnoreCase;
                PtyEnvironment = WindowsPtyEnvironment;
            }
            else if (IsMac)
            {
                PtyProviderLazy = MacProviderLazy;
                EnvironmentVariableComparer = StringComparer.Ordinal;
                PtyEnvironment = UnixPtyEnvironment;
            }
            else if (IsLinux)
            {
                PtyProviderLazy = LinuxProviderLazy;
                EnvironmentVariableComparer = StringComparer.Ordinal;
                PtyEnvironment = UnixPtyEnvironment;
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        /// <summary>
        /// Gets the <see cref="IPtyProvider"/> for the current platform.
        /// </summary>
        public static IPtyProvider PtyProvider => PtyProviderLazy.Value;

        /// <summary>
        /// Gets the comparer to determine if two environment variable keys are equivalent on the current platform.
        /// </summary>
        public static StringComparer EnvironmentVariableComparer { get; }

        /// <summary>
        /// Gets specific environment variables that are needed when spawning the PTY.
        /// </summary>
        public static IDictionary<string, string> PtyEnvironment { get; }

        private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        private static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}
