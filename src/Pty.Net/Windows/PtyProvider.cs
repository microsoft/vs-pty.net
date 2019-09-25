// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Windows
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using static Pty.Net.Windows.NativeMethods;
    using static Pty.Net.Windows.WinptyNativeInterop;

    /// <summary>
    /// Provides a pty connection for windows machines.
    /// </summary>
    internal class PtyProvider : IPtyProvider
    {
        /// <inheritdoc/>
        public Task<IPtyConnection> StartTerminalAsync(
            PtyOptions options,
            TraceSource trace,
            CancellationToken cancellationToken)
        {
            if (NativeMethods.IsPseudoConsoleSupported && !options.ForceWinPty)
            {
                return this.StartPseudoConsoleAsync(options, trace, cancellationToken);
            }
            else
            {
                return this.StartWinPtyTerminalAsync(options, trace, cancellationToken);
            }
        }

        private static void ThrowIfErrorOrNull(string message, IntPtr err, IntPtr ptr)
        {
            ThrowIfError(message, err);
            if (ptr == IntPtr.Zero)
            {
                throw new InvalidOperationException(message + ": unexpected null result");
            }
        }

        private static void ThrowIfError(string message, IntPtr error, bool alwaysThrow = false)
        {
            if (error != IntPtr.Zero)
            {
                var exceptionMessage = $"{message}: {winpty_error_msg(error)} ({winpty_error_code(error)})";
                winpty_error_free(error);
                throw new InvalidOperationException(exceptionMessage);
            }

            if (alwaysThrow)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static async Task<Stream> CreatePipeAsync(string pipeName, PipeDirection direction, CancellationToken cancellationToken)
        {
            string serverName = ".";
            if (pipeName.StartsWith("\\"))
            {
                int slash3 = pipeName.IndexOf('\\', 2);
                if (slash3 != -1)
                {
                    serverName = pipeName.Substring(2, slash3 - 2);
                }

                int slash4 = pipeName.IndexOf('\\', slash3 + 1);
                if (slash4 != -1)
                {
                    pipeName = pipeName.Substring(slash4 + 1);
                }
            }

            var pipe = new NamedPipeClientStream(serverName, pipeName, direction);
            await pipe.ConnectAsync(cancellationToken);
            return pipe;
        }

        private static string GetAppOnPath(string app, string cwd, IDictionary<string, string> env)
        {
            bool isWow64 = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") != null;
            var windir = Environment.GetEnvironmentVariable("WINDIR");
            var sysnativePath = Path.Combine(windir, "Sysnative");
            var sysnativePathWithSlash = sysnativePath + Path.DirectorySeparatorChar;
            var system32Path = Path.Combine(windir, "System32");
            var system32PathWithSlash = system32Path + Path.DirectorySeparatorChar;

            try
            {
                // If we have an absolute path then we take it.
                if (Path.IsPathRooted(app))
                {
                    if (isWow64)
                    {
                        // If path is on system32, check sysnative first
                        if (app.StartsWith(system32PathWithSlash, StringComparison.OrdinalIgnoreCase))
                        {
                            var sysnativeApp = Path.Combine(sysnativePath, app.Substring(system32PathWithSlash.Length));
                            if (File.Exists(sysnativeApp))
                            {
                                return sysnativeApp;
                            }
                        }
                    }
                    else if (app.StartsWith(sysnativePathWithSlash, StringComparison.OrdinalIgnoreCase))
                    {
                        // Change Sysnative to System32 if the OS is Windows but NOT WoW64. It's
                        // safe to assume that this was used by accident as Sysnative does not
                        // exist and will break in non-WoW64 environments.
                        return Path.Combine(system32Path, app.Substring(sysnativePathWithSlash.Length));
                    }

                    return app;
                }

                if (Path.GetDirectoryName(app) != string.Empty)
                {
                    // We have a directory and the directory is relative. Make the path absolute
                    // to the current working directory.
                    return Path.Combine(cwd, app);
                }
            }
            catch (ArgumentException)
            {
                throw new ArgumentException($"Invalid terminal app path '{app}'");
            }
            catch (PathTooLongException)
            {
                throw new ArgumentException($"Terminal app path '{app}' is too long");
            }

            string pathEnvironment = (env != null && env.TryGetValue("PATH", out string p) ? p : null)
                ?? Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrWhiteSpace(pathEnvironment))
            {
                // No PATH environment. Make path absolute to the cwd
                return Path.Combine(cwd, app);
            }

            var paths = new List<string>(pathEnvironment.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            if (isWow64)
            {
                // On Wow64, if %PATH% contains %WINDIR%\System32 but does not have %WINDIR%\Sysnative, add it before System32.
                // We do that to accomodate terminal app that VSCode may use. VSCode is a 64 bit app,
                // and to access 64 bit System32 from wow64 vsls-agent app, we need to go to sysnative.
                var indexOfSystem32 = paths.FindIndex(entry =>
                    string.Equals(entry, system32Path, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry, system32PathWithSlash, StringComparison.OrdinalIgnoreCase));

                var indexOfSysnative = paths.FindIndex(entry =>
                    string.Equals(entry, sysnativePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry, sysnativePathWithSlash, StringComparison.OrdinalIgnoreCase));

                if (indexOfSystem32 >= 0 && indexOfSysnative == -1)
                {
                    paths.Insert(indexOfSystem32, sysnativePath);
                }
            }

            // We have a simple file name. We get the path variable from the env
            // and try to find the executable on the path.
            foreach (string pathEntry in paths)
            {
                bool isPathEntryRooted;
                try
                {
                    isPathEntryRooted = Path.IsPathRooted(pathEntry);
                }
                catch (ArgumentException)
                {
                    // Ignore invalid entry on %PATH%
                    continue;
                }

                // The path entry is absolute.
                string fullPath = isPathEntryRooted ? Path.Combine(pathEntry, app) : Path.Combine(cwd, pathEntry, app);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }

                var withExtension = fullPath + ".com";
                if (File.Exists(withExtension))
                {
                    return withExtension;
                }

                withExtension = fullPath + ".exe";
                if (File.Exists(withExtension))
                {
                    return withExtension;
                }
            }

            // Not found on PATH. Make path absolute to the cwd
            return Path.Combine(cwd, app);
        }

        private static string GetEnvironmentString(IDictionary<string, string> environment)
        {
            string[] keys = new string[environment.Count];
            environment.Keys.CopyTo(keys, 0);

            string[] values = new string[environment.Count];
            environment.Values.CopyTo(values, 0);

            // Sort both by the keys
            // Windows 2000 requires the environment block to be sorted by the key.
            Array.Sort(keys, values, StringComparer.OrdinalIgnoreCase);

            // Create a list of null terminated "key=val" strings
            var result = new StringBuilder();
            for (int i = 0; i < environment.Count; ++i)
            {
                result.Append(keys[i]);
                result.Append('=');
                result.Append(values[i]);
                result.Append('\0');
            }

            // An extra null at the end indicates end of list.
            result.Append('\0');

            return result.ToString();
        }

        private async Task<IPtyConnection> StartWinPtyTerminalAsync(
           PtyOptions options,
           TraceSource trace,
           CancellationToken cancellationToken)
        {
            IntPtr error;

            IntPtr config = winpty_config_new(WINPTY_FLAG_COLOR_ESCAPES, out error);
            ThrowIfErrorOrNull("Error creating WinPTY config", error, config);

            winpty_config_set_initial_size(config, options.Cols, options.Rows);

            IntPtr handle = winpty_open(config, out error);
            winpty_config_free(config);

            ThrowIfErrorOrNull("Error launching WinPTY agent", error, handle);

            string commandLine = options.VerbatimCommandLine ?
                WindowsArguments.FormatVerbatim(options.CommandLine) :
                WindowsArguments.Format(options.CommandLine);

            string env = GetEnvironmentString(options.Environment);
            string app = GetAppOnPath(options.App, options.Cwd, options.Environment);

            trace.TraceInformation($"Starting terminal process '{app}' with command line {commandLine}");

            IntPtr spawnConfig = winpty_spawn_config_new(
                WINPTY_SPAWN_FLAG_AUTO_SHUTDOWN,
                app,
                commandLine,
                options.Cwd,
                env,
                out error);

            ThrowIfErrorOrNull("Error creating WinPTY spawn config", error, spawnConfig);

            bool spawnSuccess = winpty_spawn(handle, spawnConfig, out SafeProcessHandle hProcess, out IntPtr thread, out int procError, out error);
            winpty_spawn_config_free(spawnConfig);

            if (!spawnSuccess)
            {
                if (procError != 0)
                {
                    if (error != IntPtr.Zero)
                    {
                        winpty_error_free(error);
                    }

                    throw new InvalidOperationException($"Unable to start WinPTY terminal '{app}': {new Win32Exception(procError).Message} ({procError})");
                }

                ThrowIfError("Unable to start WinPTY terminal process", error, alwaysThrow: true);
            }

            Stream? writeToStream = null;
            Stream? readFromStream = null;
            try
            {
                writeToStream = await CreatePipeAsync(winpty_conin_name(handle), PipeDirection.Out, cancellationToken);
                readFromStream = await CreatePipeAsync(winpty_conout_name(handle), PipeDirection.In, cancellationToken);
            }
            catch
            {
                writeToStream?.Dispose();
                hProcess.Close();
                winpty_free(handle);
                throw;
            }

            return new WinPtyConnection(readFromStream, writeToStream, handle, hProcess);
        }

        private Task<IPtyConnection> StartPseudoConsoleAsync(
           PtyOptions options,
           TraceSource trace,
           CancellationToken cancellationToken)
        {
            // Create the in/out pipes
            if (!CreatePipe(out SafePipeHandle inPipePseudoConsoleSide, out SafePipeHandle inPipeOurSide, null, 0))
            {
                throw new InvalidOperationException("Could not create an anonymous pipe", new Win32Exception());
            }

            if (!CreatePipe(out SafePipeHandle outPipeOurSide, out SafePipeHandle outPipePseudoConsoleSide, null, 0))
            {
                throw new InvalidOperationException("Could not create an anonymous pipe", new Win32Exception());
            }

            var coord = new Coord(options.Cols, options.Rows);
            var pseudoConsoleHandle = new SafePseudoConsoleHandle();
            int hr;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                // Run CreatePseudoConsole* in a CER to make sure we don't leak handles.
                // MSDN suggest to put all CER code in a finally block
                // See http://msdn.microsoft.com/en-us/library/system.runtime.compilerservices.runtimehelpers.prepareconstrainedregions(v=vs.110).aspx
            }
            finally
            {
                // Create the Pseudo Console, using the pipes
                hr = CreatePseudoConsole(coord, inPipePseudoConsoleSide.Handle, outPipePseudoConsoleSide.Handle, 0, out IntPtr hPC);

                // Remember the handle inside the CER to prevent leakage
                if (hPC != IntPtr.Zero && hPC != INVALID_HANDLE_VALUE)
                {
                    pseudoConsoleHandle.InitialSetHandle(hPC);
                }
            }

            if (hr != S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            // Prepare the StartupInfoEx structure attached to the ConPTY.
            var startupInfo = default(STARTUPINFOEX);
            startupInfo.InitAttributeListAttachedToConPTY(pseudoConsoleHandle);
            IntPtr lpEnvironment = Marshal.StringToHGlobalUni(GetEnvironmentString(options.Environment));
            try
            {
                string app = GetAppOnPath(options.App, options.Cwd, options.Environment);
                string arguments = options.VerbatimCommandLine ?
                    WindowsArguments.FormatVerbatim(options.CommandLine) :
                    WindowsArguments.Format(options.CommandLine);

                var commandLine = new StringBuilder(app.Length + arguments.Length + 4);
                bool quoteApp = app.Contains(" ") && !app.StartsWith("\"") && !app.EndsWith("\"");
                if (quoteApp)
                {
                    commandLine.Append('"').Append(app).Append('"');
                }
                else
                {
                    commandLine.Append(app);
                }

                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    commandLine.Append(' ');
                    commandLine.Append(arguments);
                }

                bool success;
                int errorCode = 0;
                var processInfo = default(PROCESS_INFORMATION);
                var processHandle = new SafeProcessHandle();
                var mainThreadHandle = new SafeThreadHandle();

                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    // Run CreateProcess* in a CER to make sure we don't leak handles.
                }
                finally
                {
                    success = CreateProcess(
                        null,   // lpApplicationName
                        commandLine.ToString(),
                        null,   // lpProcessAttributes
                        null,   // lpThreadAttributes
                        false,  // bInheritHandles VERY IMPORTANT that this is false
                        EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT, // dwCreationFlags
                        lpEnvironment,
                        options.Cwd,
                        ref startupInfo,
                        out processInfo);

                    if (!success)
                    {
                        errorCode = Marshal.GetLastWin32Error();
                    }

                    // Remember the handles inside the CER to prevent leakage
                    if (processInfo.hProcess != IntPtr.Zero && processInfo.hProcess != INVALID_HANDLE_VALUE)
                    {
                        processHandle.InitialSetHandle(processInfo.hProcess);
                    }

                    if (processInfo.hThread != IntPtr.Zero && processInfo.hThread != INVALID_HANDLE_VALUE)
                    {
                        mainThreadHandle.InitialSetHandle(processInfo.hThread);
                    }
                }

                if (!success)
                {
                    var exception = new Win32Exception(errorCode);
                    throw new InvalidOperationException($"Could not start terminal process {commandLine.ToString()}: {exception.Message}", exception);
                }

                var connectionOptions = new PseudoConsoleConnection.PseudoConsoleConnectionHandles(
                    inPipePseudoConsoleSide,
                    outPipePseudoConsoleSide,
                    inPipeOurSide,
                    outPipeOurSide,
                    pseudoConsoleHandle,
                    processHandle,
                    processInfo.dwProcessId,
                    mainThreadHandle);

                var result = new PseudoConsoleConnection(connectionOptions);
                return Task.FromResult<IPtyConnection>(result);
            }
            finally
            {
                startupInfo.FreeAttributeList();
                if (lpEnvironment != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(lpEnvironment);
                }
            }
        }
    }
}
