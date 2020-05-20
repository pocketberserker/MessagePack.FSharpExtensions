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
using System.Reflection;
using System.Reflection.Emit;

namespace MessagePack.FSharp.Internal
{
    internal class DynamicAssembly
    {
        readonly AssemblyBuilder assemblyBuilder;
        readonly ModuleBuilder moduleBuilder;

        private readonly object gate = new object();

        public ModuleBuilder ModuleBuilder { get { return moduleBuilder; } }

        public DynamicAssembly(string moduleName)
        {
            AssemblyBuilderAccess builderAccess = AssemblyBuilderAccess.Run;
            this.assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(moduleName), builderAccess);
            this.moduleBuilder = this.assemblyBuilder.DefineDynamicModule(moduleName + ".dll");
        }

        /* requires lock on mono environment. see: https://github.com/neuecc/MessagePack-CSharp/issues/161 */

        public TypeBuilder DefineType(string name, TypeAttributes attr)
        {
            lock (this.gate)
            {
                return this.moduleBuilder.DefineType(name, attr);
            }
        }

        public TypeBuilder DefineType(string name, TypeAttributes attr, Type parent)
        {
            lock (this.gate)
            {
                return this.moduleBuilder.DefineType(name, attr, parent);
            }
        }

        public TypeBuilder DefineType(string name, TypeAttributes attr, Type parent, Type[] interfaces)
        {
            lock (this.gate)
            {
                return this.moduleBuilder.DefineType(name, attr, parent, interfaces);
            }
        }
    }
}