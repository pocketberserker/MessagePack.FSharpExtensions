using MessagePack.Formatters;
using Microsoft.FSharp.Control;

namespace MessagePack.FSharp.Formatters
{
    public sealed class FSharpAsyncFormatter<T> : IMessagePackFormatter<FSharpAsync<T>>
    {

        public FSharpAsyncFormatter() { }

        public void Serialize(ref MessagePackWriter writer, FSharpAsync<T> value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }
            else
            {
                var v = FSharpAsync.RunSynchronously(value, null, null);
                IFormatterResolver resolver = options.Resolver;
                resolver.GetFormatterWithVerify<T>().Serialize(ref writer, v, options);
            }
        }

        public FSharpAsync<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return default;
            }
            IFormatterResolver resolver = options.Resolver;
            options.Security.DepthStep(ref reader);
            T value = resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);;
            reader.Depth--;
            return Microsoft.FSharp.Core.ExtraTopLevelOperators.DefaultAsyncBuilder.Return(value);
        }
    }
}
