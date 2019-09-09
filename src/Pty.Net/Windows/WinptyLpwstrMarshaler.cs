// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Windows
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Marshals a LPWStr (wchar_t *) to a string without destroying the LPWStr, this is needed by winpty.
    /// </summary>
    internal class WinptyLpwstrMarshaler : ICustomMarshaler
    {
        /// <inheritdoc/>
        public object MarshalNativeToManaged(IntPtr pNativeData) => Marshal.PtrToStringUni(pNativeData);

        /// <inheritdoc/>
        public void CleanUpNativeData(IntPtr pNativeData)
        {
        }

        /// <inheritdoc/>
        public int GetNativeDataSize() => throw new NotSupportedException();

        /// <inheritdoc/>
        public IntPtr MarshalManagedToNative(object ManagedObj) => throw new NotSupportedException();

        /// <inheritdoc/>
        public void CleanUpManagedData(object ManagedObj) => throw new NotSupportedException();
    }
}
