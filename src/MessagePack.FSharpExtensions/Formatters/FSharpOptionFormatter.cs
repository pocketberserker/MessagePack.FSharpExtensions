using MessagePack.Formatters;
using Microsoft.FSharp.Core;

namespace MessagePack.FSharp.Formatters
{
    public sealed class FSharpOptionFormatter<T> : IMessagePackFormatter<FSharpOption<T>>
    {
        public void Serialize(ref MessagePackWriter writer, FSharpOption<T> value, MessagePackSerializerOptions options)
        {
            if (FSharpOption<T>.get_IsNone(value))
            {
                writer.WriteNil();
                return;
            }
            else
            {
                IFormatterResolver resolver = options.Resolver;
                resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.Value, options);
            }
        }

        public FSharpOption<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }
            IFormatterResolver resolver = options.Resolver;
            options.Security.DepthStep(ref reader);
            T value = resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);;
            reader.Depth--;
            return FSharpOption<T>.Some(value);
        }
    }

    public sealed class FSharpValueOptionFormatter<T> : IMessagePackFormatter<FSharpValueOption<T>>
    {
        public void Serialize(ref MessagePackWriter writer, FSharpValueOption<T> value, MessagePackSerializerOptions options)
        {
            if (value.IsNone)
            {
                writer.WriteNil();
                return;
            }
            else
            {
                IFormatterResolver resolver = options.Resolver;
                resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.Value, options);
            }
        }

        public FSharpValueOption<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return FSharpValueOption<T>.None;
            }
            IFormatterResolver resolver = options.Resolver;
            options.Security.DepthStep(ref reader);
            T value = resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);;
            reader.Depth--;
            return FSharpValueOption<T>.Some(value);
        }
    }
}
