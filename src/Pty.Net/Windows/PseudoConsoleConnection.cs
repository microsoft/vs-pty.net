// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Windows
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using System.Runtime.InteropServices;
    using static Pty.Net.Windows.NativeMethods;

    /// <summary>
    /// A connection to a pseudoterminal spawned by native windows APIs.
    /// </summary>
    internal sealed class PseudoConsoleConnection : IPtyConnection
    {
        private readonly Process process;
        private PseudoConsoleConnectionHandles handles;

        /// <summary>
        /// Initializes a new instance of the <see cref="PseudoConsoleConnection"/> class.
        /// </summary>
        /// <param name="handles">The set of handles associated with the pseudoconsole.</param>
        public PseudoConsoleConnection(PseudoConsoleConnectionHandles handles)
        {
            this.ReaderStream = new AnonymousPipeClientStream(PipeDirection.In, new Microsoft.Win32.SafeHandles.SafePipeHandle(handles.OutPipeOurSide.Handle, ownsHandle: false));
            this.WriterStream = new AnonymousPipeClientStream(PipeDirection.Out, new Microsoft.Win32.SafeHandles.SafePipeHandle(handles.InPipeOurSide.Handle, ownsHandle: false));

            this.handles = handles;
            this.process = Process.GetProcessById(this.Pid);
            this.process.Exited += this.Process_Exited;
            this.process.EnableRaisingEvents = true;
        }

        /// <inheritdoc/>
        public event EventHandler ProcessExited;

        /// <inheritdoc/>
        public Stream ReaderStream { get; }

        /// <inheritdoc/>
        public Stream WriterStream { get; }

        /// <inheritdoc/>
        public int Pid => this.handles.Pid;

        /// <inheritdoc/>
        public int ExitCode => this.process.ExitCode;

        /// <inheritdoc/>
        public void Dispose()
        {
            this.ReaderStream?.Dispose();
            this.WriterStream?.Dispose();

            if (this.handles != null)
            {
                this.handles.PseudoConsoleHandle.Close();
                this.handles.MainThreadHandle.Close();
                this.handles.ProcessHandle.Close();
                this.handles.InPipeOurSide.Close();
                this.handles.InPipePseudoConsoleSide.Close();
                this.handles.OutPipePseudoConsoleSide.Close();
                this.handles.OutPipeOurSide.Close();
                this.handles = null;
            }
        }

        /// <inheritdoc/>
        public void Kill()
        {
            this.process.Kill();
        }

        /// <inheritdoc/>
        public void Resize(int cols, int rows)
        {
            int hr = ResizePseudoConsole(this.handles.PseudoConsoleHandle, new Coord(cols, rows));
            if (hr != S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        /// <inheritdoc/>
        public bool WaitForExit(int milliseconds)
        {
            return this.process.WaitForExit(milliseconds);
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            this.ProcessExited?.Invoke(this, e);
        }

        /// <summary>
        /// handles to resources creates when a pseudoconsole is spawned.
        /// </summary>
        internal sealed class PseudoConsoleConnectionHandles
        {
            /// <summary>
            /// Gets or sets the input pipe on the pseudoconsole side.
            /// </summary>
            /// <remarks>
            /// This pipe is connected to <see cref="OutPipeOurSide"/>.
            /// </remarks>
            internal SafePipeHandle InPipePseudoConsoleSide { get; set; }

            /// <summary>
            /// Gets or sets the output pipe on the pseudoconsole side.
            /// </summary>
            /// <remarks>
            /// This pipe is connected to <see cref="InPipeOurSide"/>.
            /// </remarks>
            internal SafePipeHandle OutPipePseudoConsoleSide { get; set; }

            /// <summary>
            /// Gets or sets the input pipe on the local side.
            /// </summary>
            /// <remarks>
            /// This pipe is connected to <see cref="OutPipePseudoConsoleSide"/>.
            /// </remarks>
            internal SafePipeHandle InPipeOurSide { get; set; }

            /// <summary>
            /// Gets or sets the output pipe on the local side.
            /// </summary>
            /// <remarks>
            /// This pipe is connected to <see cref="InPipePseudoConsoleSide"/>.
            /// </remarks>
            internal SafePipeHandle OutPipeOurSide { get; set; }

            /// <summary>
            /// Gets or sets the handle to the pseudoconsole.
            /// </summary>
            internal SafePseudoConsoleHandle PseudoConsoleHandle { get; set; }

            /// <summary>
            /// Gets or sets the handle to the spawned process.
            /// </summary>
            internal SafeProcessHandle ProcessHandle { get; set; }

            /// <summary>
            /// Gets or sets the process ID.
            /// </summary>
            internal int Pid { get; set; }

            /// <summary>
            /// Gets or sets the handle to the main thread.
            /// </summary>
            internal SafeThreadHandle MainThreadHandle { get; set; }
        }
    }
}
