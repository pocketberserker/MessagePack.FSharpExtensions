module MessagePack.Tests.UnitTest

open Xunit

[<Fact>]
let ``unit value`` () =

  let input = ()
  let actual = convert ()
  Assert.Equal(input, actual)
