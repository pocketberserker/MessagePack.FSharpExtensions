# Benchmark

code: https://github.com/pocketberserker/MessagePack.FSharpExtensions/tree/53e63b347b1e6e36720d262940500b21ed1408ce

```
> dotnet run -c Release -p .\Benchmark\Benchmark.fsproj
Int32[] serialization test

Serialize::   MessagePack-CSharp   568.2237 ms
Deserialize::   MessagePack-CSharp   631.8417 ms

List`1 serialization test

Serialize::   MessagePack-CSharp   1056.3946 ms
Deserialize::   MessagePack-CSharp   1088.9702 ms

IUnionSample serialization test

Serialize::   MessagePack-CSharp   84.7853 ms
Deserialize::   MessagePack-CSharp   2.7591 ms

ImmutableList`1 serialization test

Serialize:: MessagePack.ImmutableCollection   6944.3367 ms
Deserialize:: MessagePack.ImmutableCollection   34904.1267 ms

ImmutableHashSet`1 serialization test

Serialize:: MessagePack.ImmutableCollection   18.5179 ms
Deserialize:: MessagePack.ImmutableCollection   4.2072 ms

ImmutableDictionary`2 serialization test

Serialize:: MessagePack.ImmutableCollection   12.2713 ms
Deserialize:: MessagePack.ImmutableCollection   3.5704 ms

FSharpList`1 serialization test

Serialize:: MessagePack.FSharpExtensions   2179.4189 ms
Deserialize:: MessagePack.FSharpExtensions   2329.7652 ms

FSharpSet`1 serialization test

Serialize:: MessagePack.FSharpExtensions   6019.5603 ms
Deserialize:: MessagePack.FSharpExtensions   93316.3144 ms

FSharpMap`2 serialization test

Serialize:: MessagePack.FSharpExtensions   7594.696 ms
Deserialize:: MessagePack.FSharpExtensions   103664.7891 ms

UnionSample serialization test

Serialize:: MessagePack.FSharpExtensions   56.2192 ms
Deserialize:: MessagePack.FSharpExtensions   1.9478 ms
```
