module MessagePack.Tests.AsyncTest

open Xunit

[<Fact>]
let ``async value`` () =

  let input = async.Return(1)
  let actual = convert input
  Assert.Equal(1, Async.RunSynchronously actual)
  