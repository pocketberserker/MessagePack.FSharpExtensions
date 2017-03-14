using System;
using System.Reflection;
using Microsoft.FSharp.Reflection;

namespace MessagePack.FSharp.Internal
{
#if NETSTANDARD
    internal static class FSharpType
    {
        private static readonly TypeInfo extensions = typeof(FSharpReflectionExtensions).GetTypeInfo();
        private static readonly MethodInfo isUnion = extensions.GetMethod("FSharpType.IsUnion.Static");
        public static readonly MethodInfo getUnionCases = extensions.GetMethod("FSharpType.GetUnionCases.Static");

        public static bool IsUnion(Type type, object fake)
        {
            return (bool)isUnion.Invoke(null, new object[] { type, fake });
        }

        public static UnionCaseInfo[] GetUnionCases(Type type, object fake)
        {
            return (UnionCaseInfo[])getUnionCases.Invoke(null, new object[] { type, fake });
        }
    }
#endif
}