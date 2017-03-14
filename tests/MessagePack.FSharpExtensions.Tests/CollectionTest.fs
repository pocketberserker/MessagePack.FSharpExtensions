module MessagePack.Tests.CollectionTest

open Xunit
open MessagePack
open MessagePack.Resolvers
open MessagePack.FSharp

let setup () =
  MessagePack.Resolvers.CompositeResolver.RegisterAndSetAsDefault(
    FSharpCollectionResolver.Instance,
    StandardResolver.Instance
  )

[<Fact>]
let ``fsharp list`` () =

  setup ()

  let input: int list = []
  let xs = MessagePackSerializer.Serialize(input)
  let actual = MessagePackSerializer.Deserialize<int list>(xs)
  Assert.Equal(box input, box actual)

  let input = [1]
  let xs = MessagePackSerializer.Serialize(input)
  let actual = MessagePackSerializer.Deserialize<int list>(xs)
  Assert.Equal(box input, box actual)

