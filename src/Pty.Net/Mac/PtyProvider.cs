// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Mac
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using static Pty.Net.Mac.NativeMethods;

    /// <summary>
    /// Provides a pty connection for MacOS machines.
    /// </summary>
    internal class PtyProvider : Unix.PtyProvider
    {
        /// <inheritdoc/>
        public override Task<IPtyConnection> StartTerminalAsync(PtyOptions options, TraceSource trace, CancellationToken cancellationToken)
        {
            var winSize = new WinSize((ushort)options.Rows, (ushort)options.Cols);

            string?[] terminalArgs = GetExecvpArgs(options);

            var controlCharacters = new Dictionary<TermSpecialControlCharacter, sbyte>
            {
                { TermSpecialControlCharacter.VEOF, 4 },
                { TermSpecialControlCharacter.VEOL, -1 },
                { TermSpecialControlCharacter.VEOL2, -1 },
                { TermSpecialControlCharacter.VERASE, 0x7f },
                { TermSpecialControlCharacter.VWERASE, 23 },
                { TermSpecialControlCharacter.VKILL, 21 },
                { TermSpecialControlCharacter.VREPRINT, 18 },
                { TermSpecialControlCharacter.VINTR, 3 },
                { TermSpecialControlCharacter.VQUIT, 0x1c },
                { TermSpecialControlCharacter.VSUSP, 26 },
                { TermSpecialControlCharacter.VSTART, 17 },
                { TermSpecialControlCharacter.VSTOP, 19 },
                { TermSpecialControlCharacter.VLNEXT, 22 },
                { TermSpecialControlCharacter.VDISCARD, 15 },
                { TermSpecialControlCharacter.VMIN, 1 },
                { TermSpecialControlCharacter.VTIME, 0 },
                { TermSpecialControlCharacter.VDSUSP, 25 },
                { TermSpecialControlCharacter.VSTATUS, 20 },
            };

            var term = new Termios(
                inputFlag: TermInputFlag.ICRNL | TermInputFlag.IXON | TermInputFlag.IXANY | TermInputFlag.IMAXBEL | TermInputFlag.BRKINT | TermInputFlag.IUTF8,
                outputFlag: TermOuptutFlag.OPOST | TermOuptutFlag.ONLCR,
                controlFlag: TermConrolFlag.CREAD | TermConrolFlag.CS8 | TermConrolFlag.HUPCL,
                localFlag: TermLocalFlag.ICANON | TermLocalFlag.ISIG | TermLocalFlag.IEXTEN | TermLocalFlag.ECHO | TermLocalFlag.ECHOE | TermLocalFlag.ECHOK | TermLocalFlag.ECHOKE | TermLocalFlag.ECHOCTL,
                speed: TermSpeed.B38400,
                controlCharacters: controlCharacters);

            int controller = 0;
            int pid = forkpty(ref controller, null, ref term, ref winSize);

            if (pid == -1)
            {
                throw new InvalidOperationException($"forkpty(4) failed with error {Marshal.GetLastWin32Error()}");
            }

            if (pid == 0)
            {
                // We are in a forked process! See http://man7.org/linux/man-pages/man2/fork.2.html for details.
                // Only our thread is running. We inherited open file descriptors and get a copy of the parent process memory.
                Environment.CurrentDirectory = options.Cwd;
                execvpe(options.App, terminalArgs, options.Environment);

                // Unreachable code after execvpe()
            }

            // We have forked the terminal
            return Task.FromResult<IPtyConnection>(new PtyConnection(controller, pid));
        }
    }
}
