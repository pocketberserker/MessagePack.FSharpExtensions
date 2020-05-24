# MessagePack.FSharpExtensions
[![NuGet Status](http://img.shields.io/nuget/v/MessagePack.FSharpExtensions.svg?style=flat)](https://www.nuget.org/packages/MessagePack.FSharpExtensions/)

MessagePack.FSharpExtensions is a [MessagePack-CSharp](https://github.com/neuecc/MessagePack-CSharp) extension library for F#.

## Usage

```fsharp
open System
open System.Buffers
open MessagePack
open MessagePack.Resolvers
open MessagePack.FSharp

[<MessagePackObject>]
type UnionSample =
  | Foo of XYZ : int
  | Bar of OPQ : string list

let convertAsMemory<'T> options (value: 'T) =
  let memory = ReadOnlyMemory(MessagePackSerializer.Serialize(value, options))
  MessagePackSerializer.Deserialize<'T>(memory, options)

let convertAsSequence<'T> options (value: 'T) =
  let sequence = ReadOnlySequence(MessagePackSerializer.Serialize(value, options))
  MessagePackSerializer.Deserialize<'T>(& sequence, options)

let dump = function
| Foo x ->
  printfn "%d" x
| Bar xs ->
  printfn "%A" xs

let resolver =
  Resolvers.CompositeResolver.Create(
    FSharpResolver.Instance,
    StandardResolver.Instance
)

let options = MessagePackSerializerOptions.Standard.WithResolver(resolver)

Foo 999
|> convertAsMemory options
|> dump

Bar ["example"]
|> convertAsSequence options
|> dump
```

## Supported types

- option
- voption
- list
- map
- set
- Discriminated Union
- Struct Discriminated Union

Records, Struct Records and Anonymous Records are serialized and deserialized using `DynamicObjectResolver` in `MessagePack-CSharp`.
