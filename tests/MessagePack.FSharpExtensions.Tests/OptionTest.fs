module MessagePack.Tests.OptionTest

open Xunit

[<Fact>]
let ``option some`` () =

  let input = Some 1
  let actual = convert input
  Assert.Equal(input, actual)

[<Fact>]
let ``option none`` () =

  let input: int option = None
  let actual = convert input
  Assert.Equal(input, actual)

[<Fact>]
let ``voption some`` () =

  let input = ValueSome 1
  let actual = convert input
  Assert.Equal(input, actual)

[<Fact>]
let ``voption none`` () =

  let input: int voption = ValueNone
  let actual = convert input
  Assert.Equal(input, actual)
