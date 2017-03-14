module MessagePack.Tests.CollectionTest

open Xunit
open MessagePack
open MessagePack.Resolvers
open MessagePack.FSharp

type WithFSharpDefaultResolver() =
  interface IFormatterResolver with
    member __.GetFormatter<'T>() =
      match FSharpCollectionResolver.Instance.GetFormatter<'T>() with
      | null -> StandardResolver.Instance.GetFormatter<'T>()
      | x -> x

let convert<'T> (value: 'T) =
  let resolver = WithFSharpDefaultResolver() :> IFormatterResolver
  MessagePackSerializer.Deserialize<'T>(MessagePackSerializer.Serialize(value, resolver), resolver)

[<Fact>]
let ``fsharp list`` () =

  let input: int list = []
  let actual = convert input
  Assert.Equal(box input, box actual)

  let input = [1]
  let actual = convert input
  Assert.Equal(box input, box actual)

[<Fact>]
let ``fsharp map`` () =

  let input= Map.empty<int, bool>
  let actual = convert input
  Assert.Equal(box input, box actual)

  let input = Map.empty |> Map.add 0 true
  let actual = convert input
  Assert.Equal(box input, box actual)

[<Fact>]
let ``fsharp set`` () =

  let input = Seq.empty<int>
  let actual = convert input
  Assert.Equal(box input, box actual)

  let input = Seq.singleton 1
  let actual = convert input
  Assert.Equal(box input, box actual)

