// Copyright (c) 2017 Yoshifumi Kawai and contributors

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Threading;

namespace MessagePack.FSharp
{
    /// <summary>
    /// Special behavior for running on the mono runtime.
    /// </summary>
    internal struct MonoProtection
    {
        /// <summary>
        /// Gets a value indicating whether the mono runtime is executing this code.
        /// </summary>
        internal static bool IsRunningOnMono => Type.GetType("Mono.Runtime") != null;

        /// <summary>
        /// A lock that we enter on mono when generating dynamic types.
        /// </summary>
        private static readonly object RefEmitLock = new object();

        /// <summary>
        /// The method to call within the expression of a <c>using</c> statement whose block surrounds all Ref.Emit code.
        /// </summary>
        /// <returns>The value to be disposed of to exit the Ref.Emit lock.</returns>
        /// <remarks>
        /// This is a no-op except when running on Mono.
        /// <see href="https://github.com/mono/mono/issues/20369#issuecomment-690316456">Mono's implementation of Ref.Emit is not thread-safe</see> so we have to lock around all use of it
        /// when using that runtime.
        /// </remarks>
        internal static MonoProtectionDisposal EnterRefEmitLock() => IsRunningOnMono ? new MonoProtectionDisposal(RefEmitLock) : default;
    }

    internal struct MonoProtectionDisposal : IDisposable
    {
        private readonly object lockObject;

        internal MonoProtectionDisposal(object lockObject)
        {
            this.lockObject = lockObject;
            Monitor.Enter(lockObject);
        }

        public void Dispose()
        {
            if (this.lockObject is object)
            {
                Monitor.Exit(this.lockObject);
            }
        }
    }
}