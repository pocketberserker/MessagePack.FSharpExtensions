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
using System.Runtime.CompilerServices;

namespace MessagePack.Internal
{
    internal static class ReflectionExtensions
    {
        public static bool IsNullable(this System.Reflection.TypeInfo type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Nullable<>);
        }

        public static bool IsPublic(this System.Reflection.TypeInfo type)
        {
            return type.IsPublic;
        }

        public static bool IsAnonymous(this System.Reflection.TypeInfo type)
        {
            return type.Namespace == null
                   && type.IsSealed
                   && (type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal)
                       || type.Name.StartsWith("<>__AnonType", StringComparison.Ordinal)
                       || type.Name.StartsWith("VB$AnonymousType_", StringComparison.Ordinal))
                   && type.IsDefined(typeof(CompilerGeneratedAttribute), false);
        }

        public static bool IsIndexer(this System.Reflection.PropertyInfo propertyInfo)
        {
            return propertyInfo.GetIndexParameters().Length > 0;
        }

        public static bool IsConstructedGenericType(this System.Reflection.TypeInfo type)
        {
            return type.AsType().IsConstructedGenericType;
        }

        public static MethodInfo GetGetMethod(this PropertyInfo propInfo)
        {
            return propInfo.GetMethod;
        }

        public static MethodInfo GetSetMethod(this PropertyInfo propInfo)
        {
            return propInfo.SetMethod;
        }
    }
}