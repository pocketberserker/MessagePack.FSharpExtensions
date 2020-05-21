module MessagePack.Tests.ClassTest

open Xunit
open System
open MessagePack
open MessagePack.Resolvers

[<MessagePackObject>]
type SimpleClass(init: int) =
  [<Key(0)>]
  let mutable inner = init

  new() = SimpleClass(0)

  [<IgnoreMember>]
  member __.Property with set(value) = inner <- value and get() = inner

let convert<'T>(value: 'T) =
  let options = MessagePackSerializerOptions.Standard.WithResolver(DynamicContractlessObjectResolverAllowPrivate.Instance)
  let bytes = MessagePackSerializer.Serialize(value, options)
  MessagePackSerializer.Deserialize<'T>(ReadOnlyMemory(bytes), options)

[<Fact>]
let ``private member`` () =

  let input = SimpleClass(1)
  let actual = convert input
  Assert.Equal(input.Property, actual.Property)
