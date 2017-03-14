using System;
using System.Collections.Generic;
using System.Reflection;
using MessagePack.Formatters;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Collections;
using MessagePack.FSharp.Formatters;

namespace MessagePack.FSharp
{
    public class FSharpResolver : IFormatterResolver
    {
        public static IFormatterResolver Instance = new FSharpResolver();

        FSharpResolver() { }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.formatter;
        }

        static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> formatter;

            static FormatterCache()
            {
                formatter = (IMessagePackFormatter<T>)FSharpGetFormatterHelper.GetFormatter(typeof(T));
            }
        }
    }

    internal static class FSharpGetFormatterHelper
    {
        static readonly Dictionary<Type, Type> formatterMap = new Dictionary<Type, Type>()
        {
              {typeof(FSharpList<>), typeof(FSharpListFormatter<>)},
              {typeof(FSharpMap<,>), typeof(FSharpMapFormatter<,>)},
              {typeof(FSharpSet<>), typeof(FSharpSetFormatter<>)},
        };

        internal static object GetFormatter(Type t)
        {
            var ti = t.GetTypeInfo();

            if (t == typeof(Unit))
            {
                return new UnitFormatter();
            }

            if (ti.IsGenericType)
            {
                var genericType = ti.GetGenericTypeDefinition();

                Type formatterType;
                if (formatterMap.TryGetValue(genericType, out formatterType))
                {
                    return CreateInstance(formatterType, ti.GenericTypeArguments);
                }
            }

            return null;
        }

        static object CreateInstance(Type genericType, Type[] genericTypeArguments, params object[] arguments)
        {
            return Activator.CreateInstance(genericType.MakeGenericType(genericTypeArguments), arguments);
        }
    }
}
