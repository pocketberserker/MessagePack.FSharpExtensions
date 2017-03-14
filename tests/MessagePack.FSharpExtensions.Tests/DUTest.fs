module MessagePack.Tests.DUTest

open Xunit
open MessagePack

[<MessagePackObject>]
type SimpleUnion =
  | A
  | B of int
  | C of int64 * float32

[<Fact>]
let ``simple DU`` () =

  let input = A
  let actual = convert input
  Assert.Equal(input, actual)

  let input = B 100
  let actual = convert input
  Assert.Equal(input, actual)

  let input = C(99999999L, -123.43f)
  let actual = convert input
  Assert.Equal(input, actual)

type StringKeyUnion = | D of Prop : int

[<Fact>]
let ``string key DU`` () =

  let input = D 1
  let actual = convert input
  Assert.Equal(input, actual)

[<Struct; MessagePackObject>]
type StructUnion =
  | E
  | F of Prop1 : int
  | G of Prop2 : int64 * Prop3: float32

[<Fact>]
let ``struct DU`` () =

  let input = E
  let actual = convert input
  Assert.Equal(input, actual)

  let input = F 100
  let actual = convert input
  Assert.Equal(input, actual)

  let input = G(99999999L, -123.43f)
  let actual = convert input
  Assert.Equal(input, actual)

[<Fact>]
let ``builtin DU(string key)`` () =

  let input: Result<int, string> = Ok 1
  let actual = convert input
  Assert.Equal(input, actual)

  let input: Result<int, string> = Error "error"
  let actual = convert input
  Assert.Equal(input, actual)
