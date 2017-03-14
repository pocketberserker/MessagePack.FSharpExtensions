using System;
using System.Collections.Generic;
using MessagePack.Formatters;
using Microsoft.FSharp.Collections;

namespace MessagePack.FSharp
{
    public class FSharpListFormatter<T> : CollectionFormatterBase<T, T[], IEnumerator<T>, FSharpList<T>>
    {
        protected override void Add(T[] collection, int index, T value)
        {
            collection[index] = value;
        }

        protected override FSharpList<T> Complete(T[] intermediateCollection)
        {
            return ListModule.OfArray(intermediateCollection);
        }

        protected override T[] Create(int count)
        {
            return new T[count];
        }

        protected override IEnumerator<T> GetSourceEnumerator(FSharpList<T> source)
        {
            return ((IEnumerable<T>)source).GetEnumerator();
        }
    }
}
