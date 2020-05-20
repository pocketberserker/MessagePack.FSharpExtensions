module MessagePack.Tests.StructDUTest

open Xunit
open MessagePack

[<Struct; MessagePackObject>]
type StructUnion =
  | E
  | F of Prop1 : int
  | G of Prop2 : int64 * Prop3: float32

[<Fact>]
let ``struct `` () =

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
let ``builtin(string key)`` () =

  let input: Result<int, string> = Ok 1
  let actual = convert input
  Assert.Equal(input, actual)

  let input: Result<int, string> = Error "error"
  let actual = convert input
  Assert.Equal(input, actual)

let mutable beforeStructCallback = false
let mutable afterStructCallback = false

[<Struct; MessagePackObject>]
type StructCallbackUnion =
  | StructCall
with
  interface IMessagePackSerializationCallbackReceiver with
    override this.OnBeforeSerialize() =
      beforeStructCallback <- true
    override this.OnAfterDeserialize() =
      afterStructCallback <- true

[<Fact>]
let ``receive callback struct union`` () =
  let input = StructCall
  convertEq input |> ignore
  Assert.True(beforeStructCallback)
  Assert.True(afterStructCallback)

module Compatibility =

  [<Union(0, typeof<CsE>)>]
  [<Union(1, typeof<CsF>)>]
  [<Union(2, typeof<CsG>)>]
  type CsStructUnion = interface end

  and [<Struct; MessagePackObject>]CsE =
    interface CsStructUnion

  and [<Struct; MessagePackObject>]CsF(item: int) =

    [<Key(0)>]
    member __.Item = item

    interface CsStructUnion

  and [<Struct; MessagePackObject>]CsG(item1: int64, item2: float32) =

    [<Key(0)>]
    member __.Item1 = item1

    [<Key(1)>]
    member __.Item2 = item2

    interface CsStructUnion

  [<Fact>]
  let ``struct `` () =

    let input = E
    let actual = convert<StructUnion, CsStructUnion> input |> box
    Assert.True(actual :? CsE)

    let input = F 100
    match convert<StructUnion, CsStructUnion> input |> box with
    | :? CsF as actual ->
      Assert.Equal(100, actual.Item)
    | actual -> Assert.True(false, sprintf "expected: CsF, but was: %A" actual)

    let input = G(99999999L, -123.43f)
    match convert<StructUnion, CsStructUnion> input |> box with
    | :? CsG as actual ->
      Assert.Equal(99999999L, actual.Item1)
      Assert.Equal(-123.43f, actual.Item2)
    | actual -> Assert.True(false, sprintf "expected: CsG, but was: %A" actual)

  [<Union(0, typeof<CsOk>)>]
  [<Union(1, typeof<CsError>)>]
  type CsResult<'T, 'U> = interface end

  and [<Struct; MessagePackObject(true)>]CsOk(resultValue: int) =

    member __.ResultValue = resultValue

    interface CsResult<int, string>

  and [<Struct; MessagePackObject(true)>]CsError(errorValue: string) =

    member __.ErrorValue = errorValue

    interface CsResult<int, string>

  [<Fact>]
  let ``builtin(string key)`` () =

    let input: Result<int, string> = Ok 1
    match convert<Result<int, string>, CsResult<int, string>> input |> box with
    | :? CsOk as actual ->
      Assert.Equal(1, actual.ResultValue)
    | actual -> Assert.True(false, sprintf "expected: CsOk, but was: %A" actual)

    let input: Result<int, string> = Error "error"
    match convert<Result<int, string>, CsResult<int, string>> input |> box with
    | :? CsError as actual ->
      Assert.Equal("error", actual.ErrorValue)
    | actual -> Assert.True(false, sprintf "expected: CsError, but was: %A" actual)
