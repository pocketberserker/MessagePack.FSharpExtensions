# MessagePack.FSharpExtensions
[![Build status](https://ci.appveyor.com/api/projects/status/bbmylbd0o5mkrptb/branch/master?svg=true)](https://ci.appveyor.com/project/pocketberserker/messagepack-fsharpextensions/branch/master)
[![Build Status](https://travis-ci.org/pocketberserker/MessagePack.FSharpExtensions.svg?branch=master)](https://travis-ci.org/pocketberserker/MessagePack.FSharpExtensions)

MessagePack.FSharpExtensions is a [MessagePack-CSharp](https://github.com/neuecc/MessagePack-CSharp) extension library for F#.

## Usage

```fsharp
open MessagePack
open MessagePack.Resolvers
open MessagePack.FSharp

CompositeResolver.RegisterAndSetAsDefault(
  FSharpResolver.Instance,
  StandardResolver.Instance
)

[<MessagePackObject>]
type UnionSample =
  | Foo of XYZ : int
  | Bar of OPQ : string list

let data = Foo 999

let bin = MessagePackSerializer.Serialize(data)

match MessagePackSerializer.Deserialize<UnionSample>(bin) with
| Foo x ->
  printfn "%d" x
| Bar xs ->
  printfn "%A" xs
```
