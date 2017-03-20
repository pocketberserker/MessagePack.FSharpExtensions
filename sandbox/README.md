# Benchmark

code: https://github.com/pocketberserker/MessagePack.FSharpExtensions/tree/53e63b347b1e6e36720d262940500b21ed1408ce

```
> dotnet run -c Release -p .\Benchmark\Benchmark.fsproj
Int32[] serialization test

Serialize::   MessagePack-CSharp   491.9719 ms
Deserialize::   MessagePack-CSharp   643.5013 ms

List`1 serialization test

Serialize::   MessagePack-CSharp   1071.1078 ms
Deserialize::   MessagePack-CSharp   1101.8369 ms

IUnionSample serialization test

Serialize::   MessagePack-CSharp   82.4559 ms
Deserialize::   MessagePack-CSharp   3.1598 ms

ImmutableList`1 serialization test

Serialize:: MessagePack.ImmutableCollection   7078.4182 ms
Deserialize:: MessagePack.ImmutableCollection   36616.2958 ms

ImmutableHashSet`1 serialization test

Serialize:: MessagePack.ImmutableCollection   18.4046 ms
Deserialize:: MessagePack.ImmutableCollection   3.3968 ms

ImmutableDictionary`2 serialization test

Serialize:: MessagePack.ImmutableCollection   11.1133 ms
Deserialize:: MessagePack.ImmutableCollection   3.0768 ms

FSharpList`1 serialization test

Serialize:: MessagePack.FSharpExtensions   2176.6548 ms
Deserialize:: MessagePack.FSharpExtensions   2494.91 ms

FSharpSet`1 serialization test

Serialize:: MessagePack.FSharpExtensions   6526.0033 ms
Deserialize:: MessagePack.FSharpExtensions   99160.0151 ms

FSharpMap`2 serialization test

Serialize:: MessagePack.FSharpExtensions   10935.7304 ms
Deserialize:: MessagePack.FSharpExtensions   107831.3949 ms

UnionSample serialization test

Serialize:: MessagePack.FSharpExtensions   62.5849 ms
Deserialize:: MessagePack.FSharpExtensions   2.3053 ms

FSharpList`1 serialization test

Serialize:: ZeroFormatter.FSharpExtensions   1787.8018 ms
Deserialize:: ZeroFormatter.FSharpExtensions   2021.9259 ms

FSharpSet`1 serialization test

Serialize:: ZeroFormatter.FSharpExtensions   7128.5002 ms
Deserialize:: ZeroFormatter.FSharpExtensions   100144.4098 ms

FSharpMap`2 serialization test

Serialize:: ZeroFormatter.FSharpExtensions   16721.4715 ms
Deserialize:: ZeroFormatter.FSharpExtensions   172189.1618 ms

UnionSample serialization test

Serialize:: ZeroFormatter.FSharpExtensions   46.8158 ms
Deserialize:: ZeroFormatter.FSharpExtensions   461.1072 ms
```
