using System;
using System.Collections.Generic;
using MessagePack.Formatters;
using Microsoft.FSharp.Collections;

namespace MessagePack.FSharp.Formatters
{
    public sealed class FSharpListFormatter<T> : CollectionFormatterBase<T, T[], IEnumerator<T>, FSharpList<T>>
    {
        protected override void Add(T[] collection, int index, T value, MessagePackSerializerOptions options)
        {
            collection[index] = value;
        }

        protected override FSharpList<T> Complete(T[] intermediateCollection)
        {
            return ListModule.OfArray(intermediateCollection);
        }

        protected override T[] Create(int count, MessagePackSerializerOptions options)
        {
            return new T[count];
        }

        protected override IEnumerator<T> GetSourceEnumerator(FSharpList<T> source)
        {
            return ((IEnumerable<T>)source).GetEnumerator();
        }
    }

    public sealed class FSharpMapFormatter<TKey, TValue> : DictionaryFormatterBase<TKey, TValue, Tuple<TKey, TValue>[], IEnumerator<KeyValuePair<TKey, TValue>>, FSharpMap<TKey, TValue>>
    {
        protected override void Add(Tuple<TKey, TValue>[] collection, int index, TKey key, TValue value, MessagePackSerializerOptions options)
        {
            collection[index] = Tuple.Create(key, value);
        }

        protected override FSharpMap<TKey, TValue> Complete(Tuple<TKey, TValue>[] intermediateCollection)
        {
            return MapModule.OfArray(intermediateCollection);
        }

        protected override Tuple<TKey, TValue>[] Create(int count, MessagePackSerializerOptions options)
        {
            return new Tuple<TKey, TValue>[count];
        }

        protected override IEnumerator<KeyValuePair<TKey, TValue>> GetSourceEnumerator(FSharpMap<TKey, TValue> source)
        {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)source).GetEnumerator();
        }
    }

    public sealed class FSharpSetFormatter<T> : CollectionFormatterBase<T, T[], IEnumerator<T>, FSharpSet<T>>
    {
        protected override void Add(T[] collection, int index, T value, MessagePackSerializerOptions options)
        {
            collection[index] = value;
        }

        protected override FSharpSet<T> Complete(T[] intermediateCollection)
        {
            return SetModule.OfArray(intermediateCollection);
        }

        protected override T[] Create(int count, MessagePackSerializerOptions options)
        {
            return new T[count];
        }

        protected override IEnumerator<T> GetSourceEnumerator(FSharpSet<T> source)
        {
            return ((IEnumerable<T>)source).GetEnumerator();
        }
    }
}
