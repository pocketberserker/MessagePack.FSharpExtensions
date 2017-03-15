module MessagePack.Tests.RecordTest

open Xunit
open MessagePack

[<MessagePackObject>]
type SimpleRecord = {
  [<Key(0)>]
  Property1: int
  [<Key(1)>]
  Property2: int64
  [<Key(2)>]
  Property3: float32
}

[<Fact>]
let ``simple record`` () =

  let input = { Property1 = 100; Property2 = 99999999L; Property3 = -123.43f }
  let actual = convert input
  Assert.Equal(input, actual)

[<MessagePackObject(true)>]
type SimpleStringKeyRecord = {
  Prop1: int
  Prop2: int64
  Prop3: float32
}

[<Fact>]
let ``string key`` () =

  let input = { Prop1 = 100; Prop2 = 99999999L; Prop3 = -123.43f }
  let actual = convert input
  Assert.Equal(input, actual)

[<Struct; MessagePackObject>]
type StructRecord = {
  [<Key(0)>]
  X: int
  [<Key(1)>]
  Y: int
}

[<Fact>]
let ``struct record`` () =

  let input = { X = 1; Y = 2 }
  let actual = convert input
  Assert.Equal(input, actual)
