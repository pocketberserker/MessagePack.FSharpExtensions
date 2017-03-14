[<AutoOpen>]
module MessagePack.Tests.Helper

open MessagePack
open MessagePack.Resolvers
open MessagePack.FSharp

type WithFSharpDefaultResolver() =
  interface IFormatterResolver with
    member __.GetFormatter<'T>() =
      match FSharpResolver.Instance.GetFormatter<'T>() with
      | null -> StandardResolver.Instance.GetFormatter<'T>()
      | x -> x

let convert<'T> (value: 'T) =
  let resolver = WithFSharpDefaultResolver() :> IFormatterResolver
  MessagePackSerializer.Deserialize<'T>(MessagePackSerializer.Serialize(value, resolver), resolver)
