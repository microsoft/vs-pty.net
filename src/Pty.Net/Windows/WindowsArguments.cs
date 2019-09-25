// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Windows
{
    using System.Linq;

    /// <summary>
    /// Helper class for formatting windows arguments when passing them to winpty and conpty.
    /// </summary>
    internal static class WindowsArguments
    {
        /// <summary>
        /// Quotes each argument before joining together.
        /// </summary>
        /// <param name="args">The command line arguments to format.</param>
        /// <returns>a space-delimited list of command line arguments, each entry surrounded by quotes.</returns>
        public static string Format(params string[]? args) =>
            string.Join(" ", (args ?? Enumerable.Empty<string>()).Select(Format));

        /// <summary>
        /// Joins the arguments together witout modification.
        /// </summary>
        /// <param name="args">The command line arguments to format.</param>
        /// <returns>A space-delimited list of command line arguments.</returns>
        public static string FormatVerbatim(params string[]? args) =>
            string.Join(" ", args ?? Enumerable.Empty<string>());

        private static string Format(string arg) =>
            string.IsNullOrEmpty(arg) ? string.Empty : "\"" + arg.Replace("\"", "\"\"") + "\"";
    }
}
