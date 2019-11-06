// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net
{
    using System;

    /// <summary>
    /// Event arguments that encapsulate data about the pty process exit.
    /// </summary>
    public class PtyExitedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PtyExitedEventArgs"/> class.
        /// </summary>
        /// <param name="exitCode">Exit code of the pty process.</param>
        internal PtyExitedEventArgs(int exitCode)
        {
            this.ExitCode = exitCode;
        }

        /// <summary>
        /// Gets or sets the exit code of the pty process.
        /// </summary>
        public int ExitCode { get; set; }
    }
}
