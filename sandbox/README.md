# Benchmark

code: https://github.com/pocketberserker/MessagePack.FSharpExtensions/tree/53e63b347b1e6e36720d262940500b21ed1408ce

```
> dotnet run -c Release -p .\Benchmark\Benchmark.fsproj
Int32[] serialization test

Serialize::   MessagePack-CSharp   523.7185 ms
Deserialize::   MessagePack-CSharp   710.0297 ms

List`1 serialization test

Serialize::   MessagePack-CSharp   1123.8954 ms
Deserialize::   MessagePack-CSharp   1295.2412 ms

IUnionSample serialization test

Serialize::   MessagePack-CSharp   88.9162 ms
Deserialize::   MessagePack-CSharp   2.5989 ms

ImmutableList`1 serialization test

Serialize:: MessagePack.ImmutableCollection   7289.519 ms
Deserialize:: MessagePack.ImmutableCollection   37143.5209 ms

ImmutableHashSet`1 serialization test

Serialize:: MessagePack.ImmutableCollection   21.0008 ms
Deserialize:: MessagePack.ImmutableCollection   3.528 ms

ImmutableDictionary`2 serialization test

Serialize:: MessagePack.ImmutableCollection   13.1161 ms
Deserialize:: MessagePack.ImmutableCollection   3.5646 ms

FSharpList`1 serialization test

Serialize:: MessagePack.FSharpExtensions   2234.1432 ms
Deserialize:: MessagePack.FSharpExtensions   2620.0237 ms

FSharpSet`1 serialization test

Serialize:: MessagePack.FSharpExtensions   6692.9768 ms
Deserialize:: MessagePack.FSharpExtensions   95083.2109 ms

FSharpMap`2 serialization test

Serialize:: MessagePack.FSharpExtensions   7996.7883 ms
Deserialize:: MessagePack.FSharpExtensions   112920.2304 ms

UnionSample serialization test

Serialize:: MessagePack.FSharpExtensions   75.3605 ms
Deserialize:: MessagePack.FSharpExtensions   2.4708 ms

FSharpList`1 serialization test

Serialize:: ZeroFormatter.FSharpExtensions   1965.5523 ms
Deserialize:: ZeroFormatter.FSharpExtensions   2177.1703 ms

FSharpSet`1 serialization test

Serialize:: ZeroFormatter.FSharpExtensions   7603.9459 ms
Deserialize:: ZeroFormatter.FSharpExtensions   122417.7596 ms

FSharpMap`2 serialization test

Serialize:: ZeroFormatter.FSharpExtensions   22411.2689 ms
Deserialize:: ZeroFormatter.FSharpExtensions   161933.0909 ms

UnionSample serialization test

Serialize:: ZeroFormatter.FSharpExtensions   32.4203 ms
Deserialize:: ZeroFormatter.FSharpExtensions   1.5194 ms
```
