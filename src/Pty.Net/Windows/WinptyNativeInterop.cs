// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Windows
{
    using System;
    using System.Runtime.InteropServices;
    using static Pty.Net.Windows.NativeMethods;

    /// <summary>
    /// Native interop definitions for winpty.
    /// </summary>
    internal static class WinptyNativeInterop
    {
        /// <summary>
        /// Relative path to the winpty.dll.
        /// </summary>
        public const string WinptyNativeDll = "winpty.dll";

        public const int WINPTY_ERROR_SUCCESS = 0;
        public const int WINPTY_ERROR_OUT_OF_MEMORY = 1;
        public const int WINPTY_ERROR_SPAWN_CREATE_PROCESS_FAILED = 2;
        public const int WINPTY_ERROR_LOST_CONNECTION = 3;
        public const int WINPTY_ERROR_AGENT_EXE_MISSING = 4;
        public const int WINPTY_ERROR_UNSPECIFIED = 5;
        public const int WINPTY_ERROR_AGENT_DIED = 6;
        public const int WINPTY_ERROR_AGENT_TIMEOUT = 7;
        public const int WINPTY_ERROR_AGENT_CREATION_FAILED = 8;

        /// <summary>
        /// Create a new screen buffer(connected to the "conerr" terminal pipe) and
        /// pass it to child processes as the STDERR handle.This flag also prevents
        /// the agent from reopening CONOUT$ when it polls -- regardless of whether the
        /// active screen buffer changes, winpty continues to monitor the original
        /// primary screen buffer.
        /// </summary>
        public const int WINPTY_FLAG_CONERR = 0x1;

        /// <summary>
        /// Don't output escape sequences.
        /// </summary>
        public const int WINPTY_FLAG_PLAIN_OUTPUT = 0x2;

        /// <summary>
        /// Do output color escape sequences.  These escapes are output by default, but
        /// are suppressed with WINPTY_FLAG_PLAIN_OUTPUT.  Use this flag to reenable
        /// them.
        /// </summary>
        public const int WINPTY_FLAG_COLOR_ESCAPES = 0x4;

        /// <summary>
        /// On XP and Vista, winpty needs to put the hidden console on a desktop in a
        /// service window station so that its polling does not interfere with other
        /// (visible) console windows.  To create this desktop, it must change the
        /// process' window station (i.e. SetProcessWindowStation) for the duration of
        /// the winpty_open call.  In theory, this change could interfere with the
        /// winpty client (e.g. other threads, spawning children), so winpty by default
        /// spawns a special agent process to create the hidden desktop.  Spawning
        /// processes on Windows is slow, though, so if
        /// WINPTY_FLAG_ALLOW_CURPROC_DESKTOP_CREATION is set, winpty changes this
        /// process' window station instead.
        /// See https://github.com/rprichard/winpty/issues/58.
        /// </summary>
        public const int WINPTY_FLAG_ALLOW_CURPROC_DESKTOP_CREATION = 0x8;

        public const int WINPTY_FLAG_MASK = 0
            | WINPTY_FLAG_CONERR
            | WINPTY_FLAG_PLAIN_OUTPUT
            | WINPTY_FLAG_COLOR_ESCAPES
            | WINPTY_FLAG_ALLOW_CURPROC_DESKTOP_CREATION;

        /// <summary>
        /// QuickEdit mode is initially disabled, and the agent does not send mouse
        /// mode sequences to the terminal.  If it receives mouse input, though, it
        /// still writes MOUSE_EVENT_RECORD values into CONIN.
        /// </summary>
        public const int WINPTY_MOUSE_MODE_NONE = 0;

        /// <summary>
        /// QuickEdit mode is initially enabled.  As CONIN enters or leaves mouse
        /// input mode (i.e. where ENABLE_MOUSE_INPUT is on and ENABLE_QUICK_EDIT_MODE
        /// is off), the agent enables or disables mouse input on the terminal.
        ///
        /// This is the default mode.
        /// </summary>
        public const int WINPTY_MOUSE_MODE_AUTO = 1;

        /// <summary>
        /// QuickEdit mode is initially disabled, and the agent enables the terminal's
        /// mouse input mode.  It does not disable terminal mouse mode (until exit).
        /// </summary>
        public const int WINPTY_MOUSE_MODE_FORCE = 2;

        /// <summary>
        /// If the spawn is marked "auto-shutdown", then the agent shuts down console
        /// output once the process exits.  The agent stops polling for new console
        /// output, and once all pending data has been written to the output pipe, the
        /// agent closes the pipe.  (At that point, the pipe may still have data in it,
        /// which the client may read.  Once all the data has been read, further reads
        /// return EOF.)
        /// </summary>
        public const int WINPTY_SPAWN_FLAG_AUTO_SHUTDOWN = 1;

        /// <summary>
        /// After the agent shuts down output, and after all output has been written
        /// into the pipe(s), exit the agent by closing the console.  If there any
        /// surviving processes still attached to the console, they are killed.
        ///
        /// Note: With this flag, an RPC call (e.g. winpty_set_size) issued after the
        /// agent exits will fail with an I/O or dead-agent error.
        /// </summary>
        public const int WINPTY_SPAWN_FLAG_EXIT_AFTER_SHUTDOWN = 2;

        /// <summary>
        /// All the spawn flags.
        /// </summary>
        public const int WINPTY_SPAWN_FLAG_MASK = 0
            | WINPTY_SPAWN_FLAG_AUTO_SHUTDOWN
            | WINPTY_SPAWN_FLAG_EXIT_AFTER_SHUTDOWN;

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int winpty_error_code(IntPtr err);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(WinptyLpwstrMarshaler))]
        public static extern string winpty_error_msg(IntPtr err);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void winpty_error_free(IntPtr err);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr winpty_config_new(ulong agentFlags, out IntPtr err);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void winpty_config_free(IntPtr cfg);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void winpty_config_set_initial_size(IntPtr cfg, int cols, int rows);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr winpty_open(IntPtr cfg, out IntPtr err);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(WinptyLpwstrMarshaler))]
        public static extern string winpty_conin_name(IntPtr wp);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(WinptyLpwstrMarshaler))]
        public static extern string winpty_conout_name(IntPtr wp);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(WinptyLpwstrMarshaler))]
        public static extern string winpty_conerr_name(IntPtr wp);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern IntPtr winpty_spawn_config_new(
            ulong spawnFlags,
            string appname,
            string cmdline,
            string cwd,
            string env,
            out IntPtr err);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void winpty_spawn_config_free(IntPtr cfg);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool winpty_spawn(
            IntPtr wp,
            IntPtr cfg,
            out SafeProcessHandle process_handle,
            out IntPtr thread_handle,
            out int create_process_error,
            out IntPtr err);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool winpty_set_size(IntPtr wp, int cols, int rows, out IntPtr err);

        [DllImport(WinptyNativeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void winpty_free(IntPtr wp);
    }
}
