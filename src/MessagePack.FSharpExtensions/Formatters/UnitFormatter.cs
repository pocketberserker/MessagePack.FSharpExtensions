using System;
using MessagePack.Formatters;
using Microsoft.FSharp.Core;

namespace MessagePack.FSharp.Formatters
{
    public class UnitFormatter : IMessagePackFormatter<Unit>
    {

        public UnitFormatter() { }

        public int Serialize(ref byte[] bytes, int offset, Unit value, IFormatterResolver formatterResolver)
        {
            return MessagePackBinary.WriteNil(ref bytes, offset);
        }

        public Unit Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            MessagePackBinary.ReadNil(bytes, offset, out readSize);
            return null;
        }
    }
}
