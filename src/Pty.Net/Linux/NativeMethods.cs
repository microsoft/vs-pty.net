// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Linux
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    internal static class NativeMethods
    {
        internal const int STDIN_FILENO = 0;

        internal const uint TIOCSIG = 0x4004_5436;
        internal const ulong TIOCSWINSZ = 0x5414;
        internal const int SIGHUP = 1;

        private const string LibSystem = "libc.so.6";
        private static readonly int SizeOfIntPtr = Marshal.SizeOf(typeof(IntPtr));

        public enum TermSpeed : uint
        {
            B38400 = 0x0F,
        }

        [Flags]
        public enum TermInputFlag : uint
        {
            BRKINT = 0x2,
            ICRNL = 0x100,
            IXON = 0x400,
            IXANY = 0x800,
            IMAXBEL = 0x2000,
            IUTF8 = 0x4000,
        }

        [Flags]
        public enum TermOuptutFlag : uint
        {
            OPOST = 1,
            ONLCR = 4,
        }

        [Flags]
        public enum TermConrolFlag : uint
        {
            CS8 = 0x30,
            CREAD = 0x80,
            HUPCL = 0x400,
        }

        [Flags]
        public enum TermLocalFlag : uint
        {
            ECHOKE = 0x800,
            ECHOE = 0x10,
            ECHOK = 0x20,
            ECHO = 0x8,
            ECHOCTL = 0x200,
            ISIG = 0x1,
            ICANON = 0x2,
            IEXTEN = 0x8000,
        }

        public enum TermSpecialControlCharacter
        {
            VEOF = 4,
            VEOL = 11,
            VEOL2 = 16,
            VERASE = 2,
            VWERASE = 14,
            VKILL = 3,
            VREPRINT = 12,
            VINTR = 0,
            VQUIT = 1,
            VSUSP = 10,
            VSTART = 8,
            VSTOP = 9,
            VLNEXT = 15,
            VDISCARD = 13,
            VMIN = 6,
            VTIME = 5,
        }

        // int cfsetispeed(struct termios *, speed_t);
        [DllImport(LibSystem)]
        internal static extern int cfsetispeed(ref Termios termios, IntPtr speed);

        // int cfsetospeed(struct termios *, speed_t);
        [DllImport(LibSystem)]
        internal static extern int cfsetospeed(ref Termios termios, IntPtr speed);

        // pid_t forkpty(int * master, char * aworker, struct termios *, struct winsize *);
        [DllImport("libutil.so.1", SetLastError = true)]
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
            // Set environment
            foreach (var environmentVariable in environment)
            {
                setenv(environmentVariable.Key, environmentVariable.Value, 1);
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

        [DllImport(LibSystem, SetLastError = true)]
        private static extern int setenv(string name, string value, int overwrite);

        // int int execvpe(const char *file, char *const argv[],char *const envp[]);
        [DllImport(LibSystem, SetLastError = true)]
        private static extern int execvp(
            [MarshalAs(UnmanagedType.LPStr)] string file,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string?[] args);

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
            public const int NCCS = 32;

            public uint IFlag;
            public uint OFlag;
            public uint CFlag;
            public uint LFlag;

            public sbyte line;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NCCS)]
            public sbyte[] CC;
            public uint ISpeed;
            public uint OSpeed;

            public Termios(
                TermInputFlag inputFlag,
                TermOuptutFlag outputFlag,
                TermConrolFlag controlFlag,
                TermLocalFlag localFlag,
                TermSpeed speed,
                IDictionary<TermSpecialControlCharacter, sbyte> controlCharacters)
            {
                this.IFlag = (uint)inputFlag;
                this.OFlag = (uint)outputFlag;
                this.CFlag = (uint)controlFlag;
                this.LFlag = (uint)localFlag;
                this.CC = new sbyte[NCCS];
                foreach (var kvp in controlCharacters)
                {
                    this.CC[(int)kvp.Key] = kvp.Value;
                }

                this.line = 0;
                this.ISpeed = 0;
                this.OSpeed = 0;
                cfsetispeed(ref this, (IntPtr)speed);
                cfsetospeed(ref this, (IntPtr)speed);
            }
        }
    }
}
