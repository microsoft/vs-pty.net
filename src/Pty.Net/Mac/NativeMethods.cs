// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Mac
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Defines native types and methods for interop with Mac OS system APIs.
    /// </summary>
    internal static class NativeMethods
    {
        internal const int STDIN_FILENO = 0;
        internal const int TCSANOW = 0;

        internal const uint TIOCSIG = 0x2000_745F;
        internal const ulong TIOCSWINSZ = 0x8008_7467;
        internal const int SIGHUP = 1;

        private const string LibSystem = "libSystem.dylib";

        private static readonly int SizeOfIntPtr = Marshal.SizeOf(typeof(IntPtr));

        public enum TermSpeed : uint
        {
            B38400 = 38400,
        }

        [Flags]
        public enum TermInputFlag : uint
        {
            /// <summary>
            /// Map BREAK to SIGINTR
            /// </summary>
            BRKINT = 0x00000002,

            /// <summary>
            /// Map CR to NL (ala CRMOD)
            /// </summary>
            ICRNL = 0x00000100,

            /// <summary>
            /// Enable output flow control
            /// </summary>
            IXON = 0x00000200,

            /// <summary>
            /// Any char will restart after stop
            /// </summary>
            IXANY = 0x00000800,

            /// <summary>
            /// Ring bell on input queue full
            /// </summary>
            IMAXBEL = 0x00002000,

            /// <summary>
            /// Maintain state for UTF-8 VERASE
            /// </summary>
            IUTF8 = 0x00004000,
        }

        [Flags]
        public enum TermOuptutFlag : uint
        {
            /// <summary>
            /// No output processing
            /// </summary>
            NONE = 0,

            /// <summary>
            /// Enable following output processing
            /// </summary>
            OPOST = 0x00000001,

            /// <summary>
            /// Map NL to CR-NL (ala CRMOD)
            /// </summary>
            ONLCR = 0x00000002,

            /// <summary>
            /// Map CR to NL
            /// </summary>
            OCRNL = 0x00000010,

            /// <summary>
            /// Don't output CR
            /// </summary>
            ONLRET = 0x00000040,
        }

        [Flags]
        public enum TermConrolFlag : uint
        {
            /// <summary>
            /// 8 bits
            /// </summary>
            CS8 = 0x00000300,

            /// <summary>
            /// Enable receiver
            /// </summary>
            CREAD = 0x00000800,

            /// <summary>
            /// Hang up on last close
            /// </summary>
            HUPCL = 0x00004000,
        }

        [Flags]
        public enum TermLocalFlag : uint
        {
            /// <summary>
            /// Visual erase for line kill
            /// </summary>
            ECHOKE = 0x00000001,

            /// <summary>
            /// Visually erase chars
            /// </summary>
            ECHOE = 0x00000002,

            /// <summary>
            /// Echo NL after line kill
            /// </summary>
            ECHOK = 0x00000004,

            /// <summary>
            /// Enable echoing
            /// </summary>
            ECHO = 0x00000008,

            /// <summary>
            /// Echo control chars as ^(Char)
            /// </summary>
            ECHOCTL = 0x00000040,

            /// <summary>
            /// Enable signals INTR, QUIT, [D]SUSP
            /// </summary>
            ISIG = 0x00000080,

            /// <summary>
            /// Canonicalize input lines
            /// </summary>
            ICANON = 0x00000100,

            /// <summary>
            /// Enable DISCARD and LNEXT
            /// </summary>
            IEXTEN = 0x00000400,
        }

        public enum TermSpecialControlCharacter
        {
            VEOF = 0,
            VEOL = 1,
            VEOL2 = 2,
            VERASE = 3,
            VWERASE = 4,
            VKILL = 5,
            VREPRINT = 6,
            VINTR = 8,
            VQUIT = 9,
            VSUSP = 10,
            VDSUSP = 11,
            VSTART = 12,
            VSTOP = 13,
            VLNEXT = 14,
            VDISCARD = 15,
            VMIN = 16,
            VTIME = 17,
            VSTATUS = 18,
        }

        // int cfsetispeed(struct termios *, speed_t);
        [DllImport(LibSystem)]
        internal static extern int cfsetispeed(ref Termios termios, IntPtr speed);

        // int cfsetospeed(struct termios *, speed_t);
        [DllImport(LibSystem)]
        internal static extern int cfsetospeed(ref Termios termios, IntPtr speed);

        // pid_t forkpty(int * master, char * aworker, struct termios *, struct winsize *);
        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int forkpty(ref int master, StringBuilder? name, ref Termios termp, ref WinSize winsize);

        // pid_t waitpid(pid_t, int *, int)
        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int waitpid(int pid, ref int status, int options);

        // int ioctl(int fd, unsigned long request, ...)
        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int ioctl(int fd, ulong request, int data);

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int ioctl(int fd, ulong request, ref WinSize winSize);

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int kill(int pid, int signal);

        internal static void execvpe(string file, string?[] args, IDictionary<string, string> environment)
        {
            if (environment != null)
            {
                // Set environment
                // As this process is going to be replaced by execvp, there is no need in freeing up the allocated memory.
                IntPtr ppEnv = Marshal.AllocHGlobal((environment.Count + 1) * SizeOfIntPtr);
                int offset = 0;
                foreach (var kvp in environment)
                {
                    IntPtr pEnv = Marshal.StringToHGlobalAnsi($"{kvp.Key}={kvp.Value}");
                    Marshal.WriteIntPtr(ppEnv, offset, pEnv);
                    offset += SizeOfIntPtr;
                }

                Marshal.WriteIntPtr(ppEnv, offset, IntPtr.Zero);

                // _NSGetEnviron() is a pointer to a pointer to an array of pointers to null-terminated strings
                Marshal.WriteIntPtr(_NSGetEnviron(), ppEnv);
            }

            if (execvp(file, args) == -1)
            {
                Environment.Exit(Marshal.GetLastWin32Error());
            }
            else
            {
                // Unreachable
                Environment.Exit(-1);
            }
        }

        // int int execvpe(const char *file, char *const argv[],char *const envp[]);
        [DllImport(LibSystem, SetLastError = true)]
        private static extern int execvp(
            [MarshalAs(UnmanagedType.LPStr)] string file,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string?[] args);

        // char ***_NSGetEnviron(void);
        [DllImport(LibSystem)]
        private static extern IntPtr _NSGetEnviron();

        [StructLayout(LayoutKind.Sequential)]
        public struct WinSize
        {
            public ushort Rows;
            public ushort Cols;
            public ushort XPixel;
            public ushort YPixel;

            public WinSize(ushort rows, ushort cols)
            {
                this.Rows = rows;
                this.Cols = cols;
                this.XPixel = 0;
                this.YPixel = 0;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Termios
        {
            public const int NCCS = 20;

            public IntPtr IFlag;
            public IntPtr OFlag;
            public IntPtr CFlag;
            public IntPtr LFlag;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NCCS)]
            public sbyte[] CC;
            public IntPtr ISpeed;
            public IntPtr OSpeed;

            public Termios(
                TermInputFlag inputFlag,
                TermOuptutFlag outputFlag,
                TermConrolFlag controlFlag,
                TermLocalFlag localFlag,
                TermSpeed speed,
                IDictionary<TermSpecialControlCharacter, sbyte> controlCharacters)
            {
                this.IFlag = (IntPtr)inputFlag;
                this.OFlag = (IntPtr)outputFlag;
                this.CFlag = (IntPtr)controlFlag;
                this.LFlag = (IntPtr)localFlag;
                this.CC = new sbyte[Termios.NCCS];
                foreach (var kvp in controlCharacters)
                {
                    this.CC[(int)kvp.Key] = kvp.Value;
                }

                this.ISpeed = IntPtr.Zero;
                this.OSpeed = IntPtr.Zero;

                cfsetispeed(ref this, (IntPtr)speed);
                cfsetospeed(ref this, (IntPtr)speed);
            }
        }
    }
}
