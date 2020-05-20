# MessagePack.FSharpExtensions
[![NuGet Status](http://img.shields.io/nuget/v/MessagePack.FSharpExtensions.svg?style=flat)](https://www.nuget.org/packages/MessagePack.FSharpExtensions/)

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
