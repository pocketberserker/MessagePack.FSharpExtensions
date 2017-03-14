using MessagePack.Formatters;
using Microsoft.FSharp.Core;

namespace MessagePack.FSharp.Formatters
{
    public class FSharpOptionFormatter<T> : IMessagePackFormatter<FSharpOption<T>>
    {
        public int Serialize(ref byte[] bytes, int offset, FSharpOption<T> value, IFormatterResolver formatterResolver)
        {
            if (FSharpOption<T>.get_IsNone(value))
            {
                return MessagePackBinary.WriteNil(ref bytes, offset);
            }
            else
            {
                return formatterResolver.GetFormatterWithVerify<T>().Serialize(ref bytes, offset, value.Value, formatterResolver);
            }
        }

        public FSharpOption<T> Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            if (MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                return null;
            }
            else
            {
                return FSharpOption<T>.Some(formatterResolver.GetFormatterWithVerify<T>().Deserialize(bytes, offset, formatterResolver, out readSize));
            }
        }
    }

    public class StaticFSharpOptionFormatter<T> : IMessagePackFormatter<FSharpOption<T>>
    {
        readonly IMessagePackFormatter<T> underlyingFormatter;

        public StaticFSharpOptionFormatter(IMessagePackFormatter<T> underlyingFormatter)
        {
            this.underlyingFormatter = underlyingFormatter;
        }

        public int Serialize(ref byte[] bytes, int offset, FSharpOption<T> value, IFormatterResolver formatterResolver)
        {
            if (FSharpOption<T>.get_IsNone(value))
            {
                return MessagePackBinary.WriteNil(ref bytes, offset);
            }
            else
            {
                return underlyingFormatter.Serialize(ref bytes, offset, value.Value, formatterResolver);
            }
        }

        public FSharpOption<T> Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            if (MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                return null;
            }
            else
            {
                return FSharpOption<T>.Some(underlyingFormatter.Deserialize(bytes, offset, formatterResolver, out readSize));
            }
        }
    }
}