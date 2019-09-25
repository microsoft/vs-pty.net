// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Options for spawning a new pty process.
    /// </summary>
    public class PtyOptions
    {
        /// <summary>
        /// Gets or sets the terminal name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the number of initial rows.
        /// </summary>
        public int Rows { get; set; }

        /// <summary>
        /// Gets or sets the number of initial columns.
        /// </summary>
        public int Cols { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the spawned process.
        /// </summary>
        public string Cwd { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the process to be spawned.
        /// </summary>
        public string App { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command line arguments to the process.
        /// </summary>
        public string[] CommandLine { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets a value indicating whether command line arguments must be quoted.
        /// <c>false</c>, the default, means that the arguments must be quoted and quotes inside escaped then concatenated with spaces.
        /// <c>true</c> means that the arguments must not be quoted and just concatenated with spaces.
        /// </summary>
        public bool VerbatimCommandLine { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether WinPty should be forced as the windows backend even on systems where ConPty is available.
        /// </summary>
        public bool ForceWinPty { get; set; }

        /// <summary>
        /// Gets or sets the process' environment variables.
        /// </summary>
        public IDictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();
    }
}
