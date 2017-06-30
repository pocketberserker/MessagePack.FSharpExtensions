# Benchmark

code: https://github.com/pocketberserker/MessagePack.FSharpExtensions/tree/08abf76ad2e035b611fcda185e72b9d0a8e53e67

```
> dotnet run -c Release -p .\Benchmark\Benchmark.fsproj
Int32[] serialization test

Serialize::   MessagePack-CSharp   532.5288 ms
Deserialize::   MessagePack-CSharp   717.8453 ms

List`1 serialization test

Serialize::   MessagePack-CSharp   1099.0243 ms
Deserialize::   MessagePack-CSharp   1302.8735 ms

IUnionSample serialization test

Serialize::   MessagePack-CSharp   123.5139 ms
Deserialize::   MessagePack-CSharp   2.8482 ms

ImmutableList`1 serialization test

Serialize:: MessagePack.ImmutableCollection   4107.3217 ms
Deserialize:: MessagePack.ImmutableCollection   31972.6376 ms

ImmutableHashSet`1 serialization test

Serialize:: MessagePack.ImmutableCollection   20.0883 ms
Deserialize:: MessagePack.ImmutableCollection   4.9541 ms

ImmutableDictionary`2 serialization test

Serialize:: MessagePack.ImmutableCollection   12.8467 ms
Deserialize:: MessagePack.ImmutableCollection   4.0862 ms

FSharpList`1 serialization test

Serialize:: MessagePack.FSharpExtensions   2001.1889 ms
Deserialize:: MessagePack.FSharpExtensions   2253.9648 ms

FSharpSet`1 serialization test

Serialize:: MessagePack.FSharpExtensions   6592.4271 ms
Deserialize:: MessagePack.FSharpExtensions   88522.4429 ms

FSharpMap`2 serialization test

Serialize:: MessagePack.FSharpExtensions   7687.7701 ms
Deserialize:: MessagePack.FSharpExtensions   106072.7271 ms

UnionSample serialization test

Serialize:: MessagePack.FSharpExtensions   79.1108 ms
Deserialize:: MessagePack.FSharpExtensions   2.2235 ms

FSharpList`1 serialization test

Serialize:: ZeroFormatter.FSharpExtensions   1600.8195 ms
Deserialize:: ZeroFormatter.FSharpExtensions   1665.3844 ms

FSharpSet`1 serialization test

Serialize:: ZeroFormatter.FSharpExtensions   7044.1311 ms
Deserialize:: ZeroFormatter.FSharpExtensions   90488.713 ms

FSharpMap`2 serialization test

Serialize:: ZeroFormatter.FSharpExtensions   13013.6842 ms
Deserialize:: ZeroFormatter.FSharpExtensions   109588.638 ms

UnionSample serialization test

Serialize:: ZeroFormatter.FSharpExtensions   38.194 ms
Deserialize:: ZeroFormatter.FSharpExtensions   2.159 ms
```
