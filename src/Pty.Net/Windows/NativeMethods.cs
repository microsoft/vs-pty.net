﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Windows
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Text;
    using Microsoft.Win32.SafeHandles;

    internal static class NativeMethods
    {
        public const int S_OK = 0;

        // dwCreationFlags for CreateProcess
        internal const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        internal const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        internal const int STARTF_USESTDHANDLES = 0x00000100;

        internal static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = new IntPtr(
            22 // ProcThreadAttributePseudoConsole
            | 0x20000); // PROC_THREAD_ATTRIBUTE_INPUT - Attribute is input only

        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private static readonly Lazy<bool> IsPseudoConsoleSupportedLazy = new Lazy<bool>(
            () =>
            {
                IntPtr hLibrary = LoadLibraryW("kernel32.dll");
                return hLibrary != IntPtr.Zero && GetProcAddress(hLibrary, "CreatePseudoConsole") != IntPtr.Zero;
            },
            isThreadSafe: true);

        internal static bool IsPseudoConsoleSupported => IsPseudoConsoleSupportedLazy.Value;

        [DllImport("kernel32.dll")]
        public static extern int GetProcessId(SafeProcessHandle hProcess);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr Attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [return: MarshalAs(UnmanagedType.Bool)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateProcess(
            string lpApplicationName,
            StringBuilder lpCommandLine,                // LPTSTR - note: CreateProcess might insert a null somewhere in this string
            SECURITY_ATTRIBUTES lpProcessAttributes,    // LPSECURITY_ATTRIBUTES
            SECURITY_ATTRIBUTES lpThreadAttributes,     // LPSECURITY_ATTRIBUTES
            bool bInheritHandles,                       // BOOL
            int dwCreationFlags,                        // DWORD
            IntPtr lpEnvironment,                       // LPVOID
            string lpCurrentDirectory,
            STARTUPINFOEX lpStartupInfo,                // LPSTARTUPINFO
            PROCESS_INFORMATION lpProcessInformation);  // LPPROCESS_INFORMATION

        [DllImport("kernel32.dll")]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal static extern int CreatePseudoConsole(Coord coord, IntPtr input, IntPtr output, uint flags, out IntPtr consoleHandle);

        [DllImport("kernel32.dll")]
        internal static extern int ResizePseudoConsole(SafePseudoConsoleHandle consoleHandle, Coord coord);

        [DllImport("kernel32.dll")]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal static extern void ClosePseudoConsole(IntPtr consoleHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [SecurityCritical]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreatePipe(
            out SafePipeHandle hReadPipe,           // PHANDLE hReadPipe,                       // read handle
            out SafePipeHandle hWritePipe,          // PHANDLE hWritePipe,                      // write handle
            SECURITY_ATTRIBUTES pipeAttributes,     // LPSECURITY_ATTRIBUTES lpPipeAttributes,  // security attributes
            int size);                              // DWORD nSize                              // pipe size

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr LoadLibraryW([MarshalAs(UnmanagedType.LPWStr)] string libName);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);

        [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Coord
        {
            public ushort X;
            public ushort Y;

            public Coord(int x, int y)
            {
                this.X = (ushort)x;
                this.Y = (ushort)y;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;

            /// <summary>
            /// Initializes the specified startup info struct with the required properties and
            /// updates its thread attribute list with the specified ConPTY handle.
            /// </summary>
            /// <param name="handle">Pseudo console handle.</param>
            internal void InitAttributeListAttachedToConPTY(SafePseudoConsoleHandle handle)
            {
                this.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
                this.StartupInfo.dwFlags = STARTF_USESTDHANDLES;

                const int AttributeCount = 1;
                var size = IntPtr.Zero;

                // Create the appropriately sized thread attribute list
                bool wasInitialized = InitializeProcThreadAttributeList(IntPtr.Zero, AttributeCount, 0, ref size);
                if (wasInitialized || size == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        $"Couldn't get the size of the process attribute list for {AttributeCount} attributes",
                        new Win32Exception());
                }

                this.lpAttributeList = Marshal.AllocHGlobal(size);
                if (this.lpAttributeList == IntPtr.Zero)
                {
                    throw new OutOfMemoryException("Couldn't reserve space for a new process attribute list");
                }

                // Set startup info's attribute list & initialize it
                wasInitialized = InitializeProcThreadAttributeList(this.lpAttributeList, AttributeCount, 0, ref size);
                if (!wasInitialized)
                {
                    throw new InvalidOperationException("Couldn't create new process attribute list", new Win32Exception());
                }

                // Set thread attribute list's Pseudo Console to the specified ConPTY
                wasInitialized = UpdateProcThreadAttribute(
                    this.lpAttributeList,
                    0,
                    PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    handle.Handle,
                    (IntPtr)Marshal.SizeOf<IntPtr>(),
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (!wasInitialized)
                {
                    throw new InvalidOperationException("Couldn't update process attribute list", new Win32Exception());
                }
            }

            internal void FreeAttributeList()
            {
                if (this.lpAttributeList != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(this.lpAttributeList);
                    this.lpAttributeList = IntPtr.Zero;
                }
            }
        }

#pragma warning disable SA1401 // Fields should be private
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        [DebuggerStepThrough]
        internal class SECURITY_ATTRIBUTES
        {
            public int nLength = 12;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle = false;
        }
#pragma warning restore SA1401 // Fields should be private

        [SecurityCritical]
        internal abstract class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            protected SafeKernelHandle(bool ownsHandle)
                : base(ownsHandle)
            {
            }

            protected SafeKernelHandle(IntPtr handle, bool ownsHandle)
                : base(ownsHandle)
            {
                this.SetHandle(handle);
            }

            public IntPtr Handle => this.handle;

            /// <summary>
            /// Use this method with the default constructor to allow the memory allocation
            /// for the handle to happen before the CER call to get it.
            /// </summary>
            /// <param name="handle">The native handle.</param>
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            public void InitialSetHandle(IntPtr handle)
            {
                this.handle = handle;
            }

            [SecurityCritical]
            protected override bool ReleaseHandle()
            {
                return NativeMethods.CloseHandle(this.handle);
            }
        }

        [SecurityCritical]
        internal sealed class SafeProcessHandle : SafeKernelHandle
        {
            public SafeProcessHandle()
                : base(true)
            {
            }

            public SafeProcessHandle(IntPtr handle, bool ownsHandle = true)
                : base(handle, ownsHandle)
            {
            }
        }

        [SecurityCritical]
        internal sealed class SafeThreadHandle : SafeKernelHandle
        {
            public SafeThreadHandle()
                : base(true)
            {
            }

            public SafeThreadHandle(IntPtr handle, bool ownsHandle = true)
                : base(handle, ownsHandle)
            {
            }
        }

        [SecurityCritical]
        internal sealed class SafePipeHandle : SafeKernelHandle
        {
            public SafePipeHandle()
                : base(ownsHandle: true)
            {
            }

            public SafePipeHandle(IntPtr handle, bool ownsHandle = true)
                : base(handle, ownsHandle)
            {
            }
        }

        [SecurityCritical]
        internal class SafePseudoConsoleHandle : SafeKernelHandle
        {
            public SafePseudoConsoleHandle()
                : base(ownsHandle: true)
            {
            }

            public SafePseudoConsoleHandle(IntPtr handle, bool ownsHandle)
                : base(ownsHandle)
            {
                this.SetHandle(handle);
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            [SecurityCritical]
            protected override bool ReleaseHandle()
            {
                NativeMethods.ClosePseudoConsole(this.handle);
                return true;
            }
        }
    }
}
