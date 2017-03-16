module Program

open System.Collections.Immutable
open MessagePack
open MessagePack.Resolvers
open MessagePack.ImmutableCollection
open MessagePack.FSharp
open Benchmark

[<MessagePackObject>]
type UnionSample =
  | Foo of XYZ : int
  | Bar of OPQ : string list

let benchmark<'T> name (target: 'T) =

  let iteration = 10000

  let mutable data: byte [] = null

  printfn "%s serialization test" typeof<'T>.Name
  printfn ""

  printf "Serialize:: "

  using (new Measure(name)) (fun _ ->
    for i in [|1..iteration|] do
      data <- MessagePackSerializer.Serialize(target)
  )

  printf "Deserialize:: "

  using (new Measure(name)) (fun _ ->
    for i in [|1..iteration|] do
      MessagePackSerializer.Deserialize<'T>(data)
      |> ignore
  )

  printfn ""

[<EntryPoint>]
let main _ =

  CompositeResolver.RegisterAndSetAsDefault(
    ImmutableCollectionResolver.Instance,
    FSharpResolver.Instance,
    StandardResolver.Instance
  )

  [|1..10000|]
  |> benchmark "MessagePack-CSharp"

  ResizeArray([|1..10000|])
  |> benchmark "MessagePack-CSharp"

  FooClass(XYZ = 99999) :> IUnionSample
  |> benchmark "MessagePack-CSharp"

  ImmutableList<int>.Empty.AddRange([|1..10000|])
  |> benchmark "MessagePack.ImmutableCollection"

  let xs = ImmutableHashSet<int>.Empty
  for i in [|1..10000|] do xs.Add(i) |> ignore
  xs
  |> benchmark "MessagePack.ImmutableCollection"

  let xs = ImmutableDictionary<int, int>.Empty
  for i in [|1..10000|] do xs.Add(i, i) |> ignore
  xs
  |> benchmark "MessagePack.ImmutableCollection"

  [1..10000]
  |> benchmark "MessagePack.FSharpExtensions"

  [|1..10000|]
  |> Set.ofArray
  |> benchmark "MessagePack.FSharpExtensions"

  [|1..10000|]
  |> Array.map (fun x -> (x, x))
  |> Map.ofArray
  |> benchmark "MessagePack.FSharpExtensions"

  Foo 99999
  |> benchmark "MessagePack.FSharpExtensions"

  0
