using MessagePack.Formatters;
using Microsoft.FSharp.Control;

namespace MessagePack.FSharp.Formatters
{
    public sealed class FSharpAsyncFormatter<T> : IMessagePackFormatter<FSharpAsync<T>>
    {

        public FSharpAsyncFormatter() { }

        public int Serialize(ref byte[] bytes, int offset, FSharpAsync<T> value, IFormatterResolver formatterResolver)
        {
            if (value == null)
            {
                return MessagePackBinary.WriteNil(ref bytes, offset);
            }
            else
            {
                var v = FSharpAsync.RunSynchronously(value, null, null);
                return formatterResolver.GetFormatterWithVerify<T>().Serialize(ref bytes, offset, v, formatterResolver);
            }
        }

        public FSharpAsync<T> Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            if (MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                return null;
            }
            else
            {
                var v = formatterResolver.GetFormatterWithVerify<T>().Deserialize(bytes, offset, formatterResolver, out readSize);
                return Microsoft.FSharp.Core.ExtraTopLevelOperators.DefaultAsyncBuilder.Return(v);
            }
        }
    }
}
