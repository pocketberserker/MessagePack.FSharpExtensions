module MessagePack.Tests.OptionTest

open Xunit

[<Fact>]
let some () =

  let input = Some 1
  let actual = convert input
  Assert.Equal(input, actual)

[<Fact>]
let none () =

  let input: int option = None
  let actual = convert input
  Assert.Equal(input, actual)
