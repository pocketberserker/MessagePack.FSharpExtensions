// Copyright (c) 2017 Yoshifumi Kawai

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using MessagePack.Formatters;
using MessagePack.Internal;
using MessagePack.FSharp.Internal;
#if !NETSTANDARD
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Reflection;
#endif

namespace MessagePack.FSharp
{
    public sealed class DynamicUnionResolver : IFormatterResolver
    {
        public static readonly DynamicUnionResolver Instance = new DynamicUnionResolver();

        const string ModuleName = "MessagePack.FSharp.DynamicUnionResolver";

        static readonly DynamicAssembly assembly;

        static int nameSequence = 0;

        DynamicUnionResolver() { }

        static DynamicUnionResolver()
        {
            assembly = new DynamicAssembly(ModuleName);
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.formatter;
        }

        static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> formatter;

            static FormatterCache()
            {
                if (!FSharpType.IsUnion(typeof(T), null)) return;

                var ti = typeof(T).GetTypeInfo();

                var formatterTypeInfo = BuildType(typeof(T));
                if (formatterTypeInfo == null) return;

                formatter = (IMessagePackFormatter<T>)Activator.CreateInstance(formatterTypeInfo.AsType());
            }
        }

        static readonly Regex SubtractFullNameRegex = new Regex(@", Version=\d+.\d+.\d+.\d+, Culture=\w+, PublicKeyToken=\w+", RegexOptions.Compiled);

        static TypeInfo BuildType(Type type)
        {
            var ti = type.GetTypeInfo();
            // order by key(important for use jump-table of switch)
            var unionCases = FSharpType.GetUnionCases(type, null).OrderBy(x => x.Tag).ToArray();

            var formatterType = typeof(IMessagePackFormatter<>).MakeGenericType(type);
            var typeBuilder = assembly.ModuleBuilder.DefineType("MessagePack.FSharp.Formatters." + SubtractFullNameRegex.Replace(type.FullName, "").Replace(".", "_") + "Formatter" + +Interlocked.Increment(ref nameSequence), TypeAttributes.Public | TypeAttributes.Sealed, null, new[] { formatterType });

            FieldBuilder keyToCaseMap = null; // Dictionary<int, UnionCaseInfo>
            var stringByteKeysFields = new FieldBuilder[unionCases.Length];

            // create map dictionary
            {
                var method = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                keyToCaseMap = typeBuilder.DefineField("keyToCaseMap", typeof(Dictionary<int, Microsoft.FSharp.Reflection.UnionCaseInfo>), FieldAttributes.Private | FieldAttributes.InitOnly);

                foreach(var unionCase in unionCases)
                {
                  stringByteKeysFields[unionCase.Tag] = typeBuilder.DefineField("stringByteKeysField" + unionCase.Tag, typeof(byte[][]), FieldAttributes.Private | FieldAttributes.InitOnly);
                }

                var il = method.GetILGenerator();
                BuildConstructor(type, unionCases, method, keyToCaseMap, stringByteKeysFields, il);
            }

            {
                var method = typeBuilder.DefineMethod("Serialize", MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                    typeof(int),
                    new Type[] { typeof(byte[]).MakeByRefType(), typeof(int), type, typeof(IFormatterResolver) });

                var il = method.GetILGenerator();
                BuildSerialize(type, unionCases, method, keyToCaseMap, il);
            }
            {
                var method = typeBuilder.DefineMethod("Deserialize", MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                    type,
                    new Type[] { typeof(byte[]), typeof(int), typeof(IFormatterResolver), typeof(int).MakeByRefType() });

                var il = method.GetILGenerator();
                BuildDeserialize(type, unionCases, method, stringByteKeysFields, il);
            }

            return typeBuilder.CreateTypeInfo();
        }

        static void BuildConstructor(Type type, Microsoft.FSharp.Reflection.UnionCaseInfo[] infos, ConstructorInfo method, FieldBuilder keyToCaseMap, FieldBuilder[] stringByteKeysFields, ILGenerator il)
        {
            il.EmitLdarg(0);
            il.Emit(OpCodes.Call, objectCtor);

            il.DeclareLocal(typeof(Microsoft.FSharp.Reflection.UnionCaseInfo []));

            il.Emit(OpCodes.Ldtoken, type);
            il.EmitCall(OpCodes.Call, typeof(Type).GetTypeInfo().GetMethod("GetTypeFromHandle"), null);
            il.Emit(OpCodes.Ldnull); // equal FSharpOpion<T>.None
            il.Emit(OpCodes.Call, getUnionCases);
            il.Emit(OpCodes.Stloc_0);

            {
                il.EmitLdarg(0);
                il.EmitLdc_I4(infos.Length);
                il.Emit(OpCodes.Newobj, caseMapDictionaryConstructor);

                var index = 0;
                foreach (var item in infos)
                {
                    il.Emit(OpCodes.Dup);
                    il.EmitLdc_I4(item.Tag);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldc_I4, index);
                    il.Emit(OpCodes.Ldelem_Ref);
                    il.EmitCall(caseMapDictionaryAdd);

                    index++;
                }

                il.Emit(OpCodes.Stfld, keyToCaseMap);
            }
            {
                foreach (var info in infos)
                {
                    var fields = info.GetFields();
                    il.EmitLdarg(0);
                    il.EmitLdc_I4(fields.Length);
                    il.Emit(OpCodes.Newarr, typeof(byte[]));

                    var index = 0;
                    foreach (var field in fields)
                    {
                        il.Emit(OpCodes.Dup);
                        il.EmitLdc_I4(index);
                        il.Emit(OpCodes.Ldstr, field.Name);
                        il.EmitCall(MessagePackBinaryTypeInfo.GetEncodedStringBytes);
                        il.Emit(OpCodes.Stelem_Ref);
                        index++;
                    }

                    il.Emit(OpCodes.Stfld, stringByteKeysFields[info.Tag]);
                }
            }

            il.Emit(OpCodes.Ret);
        }


        // int Serialize([arg:1]ref byte[] bytes, [arg:2]int offset, [arg:3]T value, [arg:4]IFormatterResolver formatterResolver);
        static void BuildSerialize(Type type, Microsoft.FSharp.Reflection.UnionCaseInfo[] infos, MethodBuilder method, FieldBuilder keyToCaseMap, ILGenerator il)
        {
            var tag = getTag(type);
            var ti = type.GetTypeInfo();

            // if(value == null) return WriteNil
            var elseBody = il.DefineLabel();
            var notFoundType = il.DefineLabel();

            il.EmitLoadArg(ti, 3);
            il.Emit(OpCodes.Brtrue_S, elseBody);
            il.Emit(OpCodes.Br, notFoundType);
            il.MarkLabel(elseBody);

            var caseInfo = il.DeclareLocal(typeof(Microsoft.FSharp.Reflection.UnionCaseInfo));

            il.EmitLoadThis();
            il.EmitLdfld(keyToCaseMap);
            il.EmitLoadArg(ti, 3);
            il.EmitCall(tag);
            il.EmitLdloca(caseInfo);
            il.EmitCall(caseMapDictionaryTryGetValue);
            il.Emit(OpCodes.Brfalse, notFoundType);

            // var startOffset = offset;
            var startOffsetLocal = il.DeclareLocal(typeof(int));
            il.EmitLdarg(2);
            il.EmitStloc(startOffsetLocal);

            // offset += WriteFixedArrayHeaderUnsafe(,,2);
            EmitOffsetPlusEqual(il, null, () =>
            {
                il.EmitLdc_I4(2);
                il.EmitCall(MessagePackBinaryTypeInfo.WriteFixedArrayHeaderUnsafe);
            });

            // offset += WriteInt32(,,value.Tag)
            EmitOffsetPlusEqual(il, null, () =>
            {
                il.EmitLoadArg(ti, 3);
                il.EmitCall(tag);
                il.EmitCall(MessagePackBinaryTypeInfo.WriteInt32);
            });

            var loopEnd = il.DefineLabel();

            // switch-case (offset += resolver.GetFormatter.Serialize(with cast)
            var switchLabels = infos.Select(x => new { Label = il.DefineLabel(), Info = x }).ToArray();
            il.EmitLoadArg(ti, 3);
            il.EmitCall(tag);
            il.Emit(OpCodes.Switch, switchLabels.Select(x => x.Label).ToArray());
            il.Emit(OpCodes.Br, loopEnd); // default

            foreach (var item in switchLabels)
            {
                il.MarkLabel(item.Label);
                EmitSerializeUnionCase(il, type, ti, UnionSerializationInfo.CreateOrNull(type, item.Info));
                il.Emit(OpCodes.Br, loopEnd);
            }

            // return startOffset- offset;
            il.MarkLabel(loopEnd);
            il.EmitLdarg(2);
            il.EmitLdloc(startOffsetLocal);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Ret);

            // else, return WriteNil
            il.MarkLabel(notFoundType);
            il.EmitLdarg(1);
            il.EmitLdarg(2);
            il.EmitCall(MessagePackBinaryTypeInfo.WriteNil);
            il.Emit(OpCodes.Ret);
        }

        // offset += ***(ref bytes, offset....
        static void EmitOffsetPlusEqual(ILGenerator il, Action loadEmit, Action emit)
        {
            il.EmitLdarg(2);

            if (loadEmit != null) loadEmit();

            il.EmitLdarg(1);
            il.EmitLdarg(2);

            emit();

            il.Emit(OpCodes.Add);
            il.EmitStarg(2);
        }

        static void EmitSerializeUnionCase(ILGenerator il, Type type, TypeInfo ti, UnionSerializationInfo info)
        {
            // IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
            if (ti.ImplementedInterfaces.Any(x => x == typeof(IMessagePackSerializationCallbackReceiver)))
            {
                // call directly
                var runtimeMethods = type.GetRuntimeMethods().Where(x => x.Name == "OnBeforeSerialize").ToArray();
                if (runtimeMethods.Length == 1)
                {
                    il.EmitLoadArg(ti, 3);
                    il.Emit(OpCodes.Call, runtimeMethods[0]); // don't use EmitCall helper(must use 'Call')
                }
                else
                {
                    il.EmitLoadArg(ti, 3);
                    if (info.IsStruct)
                    {
                        il.Emit(OpCodes.Box, type);
                    }
                    il.EmitCall(onBeforeSerialize);
                }
            }

            if (info.IsIntKey)
            {
                // use Array
                var maxKey = info.Members.Select(x => x.IntKey).DefaultIfEmpty(0).Max();
                var intKeyMap = info.Members.ToDictionary(x => x.IntKey);

                EmitOffsetPlusEqual(il, null, () =>
                {
                    var len = maxKey + 1;
                    il.EmitLdc_I4(len);
                    if (len <= MessagePackRange.MaxFixArrayCount)
                    {
                        il.EmitCall(MessagePackBinaryTypeInfo.WriteFixedArrayHeaderUnsafe);
                    }
                    else
                    {
                        il.EmitCall(MessagePackBinaryTypeInfo.WriteArrayHeader);
                    }
                });

                for (int i = 0; i <= maxKey; i++)
                {
                    UnionSerializationInfo.EmittableMember member;
                    if (intKeyMap.TryGetValue(i, out member))
                    {
                        // offset += serialzie
                        EmitSerializeValue(il, ti, member);
                    }
                    else
                    {
                        // Write Nil as Blanc
                        EmitOffsetPlusEqual(il, null, () =>
                        {
                            il.EmitCall(MessagePackBinaryTypeInfo.WriteNil);
                        });
                    }
                }
            }
            else
            {
                // use Map
                var writeCount = info.Members.Count();

                EmitOffsetPlusEqual(il, null, () =>
                {
                    il.EmitLdc_I4(writeCount);
                    if (writeCount <= MessagePackRange.MaxFixMapCount)
                    {
                        il.EmitCall(MessagePackBinaryTypeInfo.WriteFixedMapHeaderUnsafe);
                    }
                    else
                    {
                        il.EmitCall(MessagePackBinaryTypeInfo.WriteMapHeader);
                    }
                });

                foreach (var item in info.Members)
                {
                    // offset += writekey
                    if (info.IsStringKey)
                    {
                        EmitOffsetPlusEqual(il, null, () =>
                        {
                            // embed string and bytesize
                            il.Emit(OpCodes.Ldstr, item.StringKey);
                            il.EmitLdc_I4(StringEncoding.UTF8.GetByteCount(item.StringKey));
                            il.EmitCall(MessagePackBinaryTypeInfo.WriteStringUnsafe);
                        });
                    }

                    // offset += serialzie
                    EmitSerializeValue(il, ti, item);
                }
            }
        }

        static void EmitSerializeValue(ILGenerator il, TypeInfo type, UnionSerializationInfo.EmittableMember member)
        {
            var t = member.Type;
            if (IsOptimizeTargetType(t))
            {
                EmitOffsetPlusEqual(il, null, () =>
                {
                    il.EmitLoadArg(type, 3);
                    member.EmitLoadValue(il);
                    if (t == typeof(byte[]))
                    {
                        il.EmitCall(MessagePackBinaryTypeInfo.WriteBytes);
                    }
                    else
                    {
                        il.EmitCall(MessagePackBinaryTypeInfo.TypeInfo.GetDeclaredMethods("Write" + t.Name).OrderByDescending(x => x.GetParameters().Length).First());
                    }
                });
            }
            else
            {
                EmitOffsetPlusEqual(il, () =>
                {
                    il.EmitLdarg(4);
                    il.Emit(OpCodes.Call, getFormatterWithVerify.MakeGenericMethod(t));
                }, () =>
                {
                    il.EmitLoadArg(type, 3);
                    member.EmitLoadValue(il);
                    il.EmitLdarg(4);
                    il.EmitCall(getSerialize(t));
                });
            }
        }

        // T Deserialize([arg:1]byte[] bytes, [arg:2]int offset, [arg:3]IFormatterResolver formatterResolver, [arg:4]out int readSize);
        static void BuildDeserialize(Type type, Microsoft.FSharp.Reflection.UnionCaseInfo[] infos, MethodBuilder method, FieldBuilder[] stringByteKeysFields, ILGenerator il)
        {
            // if(MessagePackBinary.IsNil) readSize = 1, return null;
            var falseLabel = il.DefineLabel();
            il.EmitLdarg(1);
            il.EmitLdarg(2);
            il.EmitCall(MessagePackBinaryTypeInfo.IsNil);
            il.Emit(OpCodes.Brfalse_S, falseLabel);

            if (type.GetTypeInfo().IsClass)
            {
                il.EmitLdarg(4);
                il.EmitLdc_I4(1);
                il.Emit(OpCodes.Stind_I4);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ret);
            }
            else
            {
                il.Emit(OpCodes.Ldstr, "typecode is null, struct not supported");
                il.Emit(OpCodes.Newobj, invalidOperationExceptionConstructor);
                il.Emit(OpCodes.Throw);
            }

            // read-array header and validate, ReadArrayHeader(bytes, offset, out readSize) != 2) throw;
            il.MarkLabel(falseLabel);
            var startOffset = il.DeclareLocal(typeof(int));
            il.EmitLdarg(2);
            il.EmitStloc(startOffset);

            var rightLabel = il.DefineLabel();
            il.EmitLdarg(1);
            il.EmitLdarg(2);
            il.EmitLdarg(4);
            il.EmitCall(MessagePackBinaryTypeInfo.ReadArrayHeader);
            il.EmitLdc_I4(2);
            il.Emit(OpCodes.Beq_S, rightLabel);
            il.Emit(OpCodes.Ldstr, "Invalid Union data was detected. Type:" + type.FullName);
            il.Emit(OpCodes.Newobj, invalidOperationExceptionConstructor);
            il.Emit(OpCodes.Throw);

            il.MarkLabel(rightLabel);
            EmitOffsetPlusReadSize(il);

            // read key
            var key = il.DeclareLocal(typeof(int));
            il.EmitLdarg(1);
            il.EmitLdarg(2);
            il.EmitLdarg(4);
            il.EmitCall(MessagePackBinaryTypeInfo.ReadInt32);
            il.EmitStloc(key);
            EmitOffsetPlusReadSize(il);

            // switch->read
            var result = il.DeclareLocal(type);
            var loopEnd = il.DefineLabel();
            il.Emit(OpCodes.Ldnull);
            il.EmitStloc(result);
            il.Emit(OpCodes.Ldloc, key);

            var switchLabels = infos.Select(x => new { Label = il.DefineLabel(), Info = x }).ToArray();
            il.Emit(OpCodes.Switch, switchLabels.Select(x => x.Label).ToArray());

            // default
            il.EmitLdarg(2);
            il.EmitLdarg(1);
            il.EmitLdarg(2);
            il.EmitCall(MessagePackBinaryTypeInfo.ReadNextBlock);
            il.Emit(OpCodes.Add);
            il.EmitStarg(2);
            il.Emit(OpCodes.Br, loopEnd);

            foreach (var item in switchLabels)
            {
                il.MarkLabel(item.Label);
                EmitDeserializeUnionCase(il, type, UnionSerializationInfo.CreateOrNull(type, item.Info), key, stringByteKeysFields[item.Info.Tag]);
                il.Emit(OpCodes.Stloc, result);
                il.Emit(OpCodes.Br, loopEnd);
            }

            il.MarkLabel(loopEnd);

            // finish readSize = offset - startOffset;
            il.EmitLdarg(4);
            il.EmitLdarg(2);
            il.EmitLdloc(startOffset);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Stind_I4);
            il.Emit(OpCodes.Ldloc, result);
            il.Emit(OpCodes.Ret);
        }

        static void EmitOffsetPlusReadSize(ILGenerator il)
        {
            il.EmitLdarg(2);
            il.EmitLdarg(4);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Add);
            il.EmitStarg(2);
        }

        static void EmitDeserializeUnionCase(ILGenerator il, Type type, UnionSerializationInfo info, LocalBuilder unionKey, FieldBuilder stringByteKeysField)
        {
            // var startOffset = offset;
            var startOffsetLocal = il.DeclareLocal(typeof(int)); // [loc:0]
            il.EmitLdarg(2);
            il.EmitStloc(startOffsetLocal);

            // var length = ReadMapHeader
            var length = il.DeclareLocal(typeof(int));
            il.EmitLdarg(1);
            il.EmitLdarg(2);
            il.EmitLdarg(4);

            if (info.IsIntKey)
            {
                il.EmitCall(MessagePackBinaryTypeInfo.ReadArrayHeader);
            }
            else
            {
                il.EmitCall(MessagePackBinaryTypeInfo.ReadMapHeader);
            }
            il.EmitStloc(length);
            EmitOffsetPlusReadSize(il);

            // make local fields
            Label? gotoDefault = null;
            DeserializeInfo[] infoList;
            if (info.IsIntKey)
            {
                var maxKey = info.Members.Select(x => x.IntKey).DefaultIfEmpty(-1).Max();
                var len = maxKey + 1;
                var intKeyMap = info.Members.ToDictionary(x => x.IntKey);

                infoList = Enumerable.Range(0, len)
                    .Select(x =>
                    {
                        UnionSerializationInfo.EmittableMember member;
                        if (intKeyMap.TryGetValue(x, out member))
                        {
                            return new DeserializeInfo
                            {
                                MemberInfo = member,
                                LocalField = il.DeclareLocal(member.Type),
                                SwitchLabel = il.DefineLabel()
                            };
                        }
                        else
                        {
                            // return null MemberInfo, should filter null
                            if (gotoDefault == null)
                            {
                                gotoDefault = il.DefineLabel();
                            }
                            return new DeserializeInfo
                            {
                                MemberInfo = null,
                                LocalField = null,
                                SwitchLabel = gotoDefault.Value,
                            };
                        }
                    })
                    .ToArray();
            }
            else
            {
                infoList = info.Members
                    .Select(item => new DeserializeInfo
                    {
                        MemberInfo = item,
                        LocalField = il.DeclareLocal(item.Type),
                        SwitchLabel = il.DefineLabel()
                    })
                    .ToArray();
            }

            // Read Loop(for var i = 0; i< length; i++)
            if (info.IsStringKey)
            {
                var automata = new AutomataDictionary();
                for (int i = 0; i < info.Members.Length; i++)
                {
                    automata.Add(info.Members[i].StringKey, i);
                }

                var buffer = il.DeclareLocal(typeof(byte).MakeByRefType(), true);
                var keyArraySegment = il.DeclareLocal(typeof(ArraySegment<byte>));
                var longKey = il.DeclareLocal(typeof(ulong));
                var p = il.DeclareLocal(typeof(byte*));
                var rest = il.DeclareLocal(typeof(int));

                // fixed (byte* buffer = &bytes[0]) {
                il.EmitLdarg(1);
                il.EmitLdc_I4(0);
                il.Emit(OpCodes.Ldelema, typeof(byte));
                il.EmitStloc(buffer);

                // for (int i = 0; i < len; i++)
                il.EmitIncrementFor(length, forILocal =>
                {
                    var readNext = il.DefineLabel();
                    var loopEnd = il.DefineLabel();

                    il.EmitLdarg(1);
                    il.EmitLdarg(2);
                    il.EmitLdarg(4);
                    il.EmitCall(MessagePackBinaryTypeInfo.ReadStringSegment);
                    il.EmitStloc(keyArraySegment);
                    EmitOffsetPlusReadSize(il);

                    // p = buffer + arraySegment.Offset
                    il.EmitLdloc(buffer);
                    il.Emit(OpCodes.Conv_I);
                    il.EmitLdloca(keyArraySegment);
                    il.EmitCall(typeof(ArraySegment<byte>).GetRuntimeProperty("Offset").GetGetMethod());
                    il.Emit(OpCodes.Add);
                    il.EmitStloc(p);

                    // rest = arraySegment.Count
                    il.EmitLdloca(keyArraySegment);
                    il.EmitCall(typeof(ArraySegment<byte>).GetRuntimeProperty("Count").GetGetMethod());
                    il.EmitStloc(rest);

                    // if(rest == 0) goto End
                    il.EmitLdloc(rest);
                    il.Emit(OpCodes.Brfalse, readNext);

                    // gen automata name lookup
                    automata.EmitMatch(il, p, rest, longKey, x =>
                    {
                        var i = x.Value;
                        if (infoList[i].MemberInfo != null)
                        {
                            EmitDeserializeValue(il, infoList[i]);
                            il.Emit(OpCodes.Br, loopEnd);
                        }
                        else
                        {
                            il.Emit(OpCodes.Br, readNext);
                        }
                    }, () =>
                    {
                        il.Emit(OpCodes.Br, readNext);
                    });

                    il.MarkLabel(readNext);
                    il.EmitLdarg(4);
                    il.EmitLdarg(1);
                    il.EmitLdarg(2);
                    il.EmitCall(MessagePackBinaryTypeInfo.ReadNextBlock);
                    il.Emit(OpCodes.Stind_I4);

                    il.MarkLabel(loopEnd);
                    EmitOffsetPlusReadSize(il);
                });

                // end fixed
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Conv_U);
                il.EmitStloc(buffer);
            }
            else
            {
                var key = il.DeclareLocal(typeof(int));
                var switchDefault = il.DefineLabel();

                il.EmitIncrementFor(length, forILocal =>
                {
                    var loopEnd = il.DefineLabel();

                    il.EmitLdloc(forILocal);
                    il.EmitStloc(key);

                    // switch... local = Deserialize
                    il.EmitLdloc(key);

                    il.Emit(OpCodes.Switch, infoList.Select(x => x.SwitchLabel).ToArray());

                    il.MarkLabel(switchDefault);
                    // default, only read. readSize = MessagePackBinary.ReadNextBlock(bytes, offset);
                    il.EmitLdarg(4);
                    il.EmitLdarg(1);
                    il.EmitLdarg(2);
                    il.EmitCall(MessagePackBinaryTypeInfo.ReadNextBlock);
                    il.Emit(OpCodes.Stind_I4);
                    il.Emit(OpCodes.Br, loopEnd);

                    if (gotoDefault != null)
                    {
                        il.MarkLabel(gotoDefault.Value);
                        il.Emit(OpCodes.Br, switchDefault);
                    }

                    foreach (var item in infoList)
                    {
                        if (item.MemberInfo != null)
                        {
                            il.MarkLabel(item.SwitchLabel);
                            EmitDeserializeValue(il, item);
                            il.Emit(OpCodes.Br, loopEnd);
                        }
                    }

                    // offset += readSize
                    il.MarkLabel(loopEnd);
                    EmitOffsetPlusReadSize(il);
                });
            }

            // finish readSize: readSize = offset - startOffset;
            il.EmitLdarg(4);
            il.EmitLdarg(2);
            il.EmitLdloc(startOffsetLocal);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Stind_I4);

            // create result union case
            var structLocal = EmitNewObject(il, type, info, infoList);

            // IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
            if (type.GetTypeInfo().ImplementedInterfaces.Any(x => x == typeof(IMessagePackSerializationCallbackReceiver)))
            {
                // call directly
                var runtimeMethods = type.GetRuntimeMethods().Where(x => x.Name == "OnAfterDeserialize").ToArray();
                if (runtimeMethods.Length == 1)
                {
                    if (info.IsClass)
                    {
                        il.Emit(OpCodes.Dup);
                    }
                    else
                    {
                        il.EmitLdloca(structLocal);
                    }

                    il.Emit(OpCodes.Call, runtimeMethods[0]); // don't use EmitCall helper(must use 'Call')
                }
                else
                {
                    if (info.IsStruct)
                    {
                        il.EmitLdloc(structLocal);
                        il.Emit(OpCodes.Box, type);
                    }
                    else
                    {
                        il.Emit(OpCodes.Dup);
                    }
                    il.EmitCall(onAfterDeserialize);
                }
            }

            if (info.IsStruct)
            {
                il.Emit(OpCodes.Ldloc, structLocal);
            }
        }

        static void EmitDeserializeValue(ILGenerator il, DeserializeInfo info)
        {
            var member = info.MemberInfo;
            var t = member.Type;
            if (IsOptimizeTargetType(t))
            {
                il.EmitLdarg(1);
                il.EmitLdarg(2);
                il.EmitLdarg(4);
                if (t == typeof(byte[]))
                {
                    il.EmitCall(MessagePackBinaryTypeInfo.ReadBytes);
                }
                else
                {
                    il.EmitCall(MessagePackBinaryTypeInfo.TypeInfo.GetDeclaredMethods("Read" + t.Name).OrderByDescending(x => x.GetParameters().Length).First());
                }
            }
            else
            {
                il.EmitLdarg(3);
                il.EmitCall(getFormatterWithVerify.MakeGenericMethod(t));
                il.EmitLdarg(1);
                il.EmitLdarg(2);
                il.EmitLdarg(3);
                il.EmitLdarg(4);
                il.EmitCall(getDeserialize(t));
            }

            il.EmitStloc(info.LocalField);
        }

        static LocalBuilder EmitNewObject(ILGenerator il, Type type, UnionSerializationInfo info, DeserializeInfo[] members)
        {
            if (info.IsClass)
            {
                foreach (var item in info.MethodParameters)
                {
                    var local = members.First(x => x.MemberInfo == item);
                    il.EmitLdloc(local.LocalField);
                }

                il.Emit(OpCodes.Call, info.NewMethod);

                return null;
            }
            else
            {
                var result = il.DeclareLocal(type);
                foreach (var item in info.MethodParameters)
                {
                    var local = members.First(x => x.MemberInfo == item);
                    il.EmitLdloc(local.LocalField);
                }

                il.Emit(OpCodes.Call, info.NewMethod);
                il.Emit(OpCodes.Stloc, result);

                return result; // struct returns local result field
            }
        }

        // https://github.com/neuecc/MessagePack-CSharp/blob/v1.1.1/src/MessagePack/Resolvers/DynamicObjectResolver.cs#L704
        static bool IsOptimizeTargetType(Type type)
        {
            if (type == typeof(Int16)
             || type == typeof(Int32)
             || type == typeof(Int64)
             || type == typeof(UInt16)
             || type == typeof(UInt32)
             || type == typeof(UInt64)
             || type == typeof(Single)
             || type == typeof(Double)
             || type == typeof(bool)
             || type == typeof(byte)
             || type == typeof(sbyte)
             || type == typeof(char)
             // not includes DateTime and String and Binary.
             //|| type == typeof(DateTime)
             //|| type == typeof(string)
             //|| type == typeof(byte[])
             )
            {
                return true;
            }
            return false;
        }

        // EmitInfos...

        static readonly Type refByte = typeof(byte[]).MakeByRefType();
        static readonly Type refInt = typeof(int).MakeByRefType();
        static readonly Type refUnionCaseInfo = typeof(Microsoft.FSharp.Reflection.UnionCaseInfo).MakeByRefType();
        static readonly MethodInfo getFormatterWithVerify = typeof(FormatterResolverExtensions).GetRuntimeMethods().First(x => x.Name == "GetFormatterWithVerify");

        static readonly Func<Type, MethodInfo> getSerialize = t => typeof(IMessagePackFormatter<>).MakeGenericType(t).GetRuntimeMethod("Serialize", new[] { refByte, typeof(int), t, typeof(IFormatterResolver) });
        static readonly Func<Type, MethodInfo> getDeserialize = t => typeof(IMessagePackFormatter<>).MakeGenericType(t).GetRuntimeMethod("Deserialize", new[] { typeof(byte[]), typeof(int), typeof(IFormatterResolver), refInt });

        static readonly ConstructorInfo caseMapDictionaryConstructor = typeof(Dictionary<int, Microsoft.FSharp.Reflection.UnionCaseInfo>).GetTypeInfo().DeclaredConstructors.First(x => { var p = x.GetParameters(); return p.Length == 1 && p[0].ParameterType == typeof(int); });
        static readonly MethodInfo caseMapDictionaryAdd = typeof(Dictionary<int, Microsoft.FSharp.Reflection.UnionCaseInfo>).GetRuntimeMethod("Add", new[] { typeof(int), typeof(Microsoft.FSharp.Reflection.UnionCaseInfo) });
        static readonly MethodInfo caseMapDictionaryTryGetValue = typeof(Dictionary<int, Microsoft.FSharp.Reflection.UnionCaseInfo>).GetRuntimeMethod("TryGetValue", new[] { typeof(int), refUnionCaseInfo });

        static readonly ConstructorInfo invalidOperationExceptionConstructor = typeof(System.InvalidOperationException).GetTypeInfo().DeclaredConstructors.First(x => { var p = x.GetParameters(); return p.Length == 1 && p[0].ParameterType == typeof(string); });
        static readonly ConstructorInfo objectCtor = typeof(object).GetTypeInfo().DeclaredConstructors.First(x => x.GetParameters().Length == 0);

        static readonly Func<Type, MethodInfo> getTag = type => type.GetTypeInfo().GetProperty("Tag").GetGetMethod();
        static readonly MethodInfo getUnionCases =
#if NETSTANDARD
            FSharpType.getUnionCases;
#else
            typeof(Microsoft.FSharp.Reflection.FSharpType).GetTypeInfo().GetMethod("GetUnionCases", new Type[] { typeof(Type), typeof(FSharpOption<BindingFlags>)});
#endif

        static readonly MethodInfo onBeforeSerialize = typeof(IMessagePackSerializationCallbackReceiver).GetRuntimeMethod("OnBeforeSerialize", Type.EmptyTypes);
        static readonly MethodInfo onAfterDeserialize = typeof(IMessagePackSerializationCallbackReceiver).GetRuntimeMethod("OnAfterDeserialize", Type.EmptyTypes);

        static class MessagePackBinaryTypeInfo
        {
            public static TypeInfo TypeInfo = typeof(MessagePackBinary).GetTypeInfo();

            public static readonly MethodInfo GetEncodedStringBytes = typeof(MessagePackBinary).GetRuntimeMethod("GetEncodedStringBytes", new[] { typeof(string) });
            public static MethodInfo WriteFixedMapHeaderUnsafe = typeof(MessagePackBinary).GetRuntimeMethod("WriteFixedMapHeaderUnsafe", new[] { refByte, typeof(int), typeof(int) });
            public static MethodInfo WriteFixedArrayHeaderUnsafe = typeof(MessagePackBinary).GetRuntimeMethod("WriteFixedArrayHeaderUnsafe", new[] { refByte, typeof(int), typeof(int) });
            public static MethodInfo WriteMapHeader = typeof(MessagePackBinary).GetRuntimeMethod("WriteMapHeader", new[] { refByte, typeof(int), typeof(int) });
            public static MethodInfo WriteArrayHeader = typeof(MessagePackBinary).GetRuntimeMethod("WriteArrayHeader", new[] { refByte, typeof(int), typeof(int) });
            public static MethodInfo WritePositiveFixedIntUnsafe = typeof(MessagePackBinary).GetRuntimeMethod("WritePositiveFixedIntUnsafe", new[] { refByte, typeof(int), typeof(int) });
            public static MethodInfo WriteInt32 = typeof(MessagePackBinary).GetRuntimeMethod("WriteInt32", new[] { refByte, typeof(int), typeof(int) });
            public static MethodInfo WriteBytes = typeof(MessagePackBinary).GetRuntimeMethod("WriteBytes", new[] { refByte, typeof(int), typeof(byte[]) });
            public static MethodInfo WriteNil = typeof(MessagePackBinary).GetRuntimeMethod("WriteNil", new[] { refByte, typeof(int) });
            public static MethodInfo ReadBytes = typeof(MessagePackBinary).GetRuntimeMethod("ReadBytes", new[] { typeof(byte[]), typeof(int), refInt });
            public static MethodInfo ReadInt32 = typeof(MessagePackBinary).GetRuntimeMethod("ReadInt32", new[] { typeof(byte[]), typeof(int), refInt });
            public static MethodInfo ReadStringSegment = typeof(MessagePackBinary).GetRuntimeMethod("ReadStringSegment", new[] { typeof(byte[]), typeof(int), refInt });
            public static MethodInfo IsNil = typeof(MessagePackBinary).GetRuntimeMethod("IsNil", new[] { typeof(byte[]), typeof(int) });
            public static MethodInfo ReadNextBlock = typeof(MessagePackBinary).GetRuntimeMethod("ReadNextBlock", new[] { typeof(byte[]), typeof(int) });
            public static MethodInfo WriteStringUnsafe = typeof(MessagePackBinary).GetRuntimeMethod("WriteStringUnsafe", new[] { refByte, typeof(int), typeof(string), typeof(int) });

            public static MethodInfo ReadArrayHeader = typeof(MessagePackBinary).GetRuntimeMethod("ReadArrayHeader", new[] { typeof(byte[]), typeof(int), refInt });
            public static MethodInfo ReadMapHeader = typeof(MessagePackBinary).GetRuntimeMethod("ReadMapHeader", new[] { typeof(byte[]), typeof(int), refInt });

            static MessagePackBinaryTypeInfo()
            {
            }
        }

        class DeserializeInfo
        {
            public UnionSerializationInfo.EmittableMember MemberInfo { get; set; }
            public LocalBuilder LocalField { get; set; }
            public Label SwitchLabel { get; set; }
        }
    }
}

namespace MessagePack.FSharp.Internal
{
    internal class UnionSerializationInfo
    {
        public bool IsIntKey { get; set; }
        public bool IsStringKey { get { return !IsIntKey; } }
        public bool IsClass { get; set; }
        public bool IsStruct { get { return !IsClass; } }
        public MethodInfo NewMethod { get; set; }
        public EmittableMember[] MethodParameters { get; set; }
        public EmittableMember[] Members { get; set; }

        UnionSerializationInfo() { }

        public static UnionSerializationInfo CreateOrNull(Type type, Microsoft.FSharp.Reflection.UnionCaseInfo caseInfo)
        {
            var ti = type.GetTypeInfo();
            var isClass = ti.IsClass;

            var contractAttr = ti.GetCustomAttribute<MessagePackObjectAttribute>();

            var isIntKey = true;
            var intMembers = new Dictionary<int, EmittableMember>();
            var stringMembers = new Dictionary<string, EmittableMember>();

            if (contractAttr == null || contractAttr.KeyAsPropertyName)
            {
                isIntKey = false;

                var hiddenIntKey = 0;
                foreach (var item in caseInfo.GetFields())
                {
                    var member = new EmittableMember
                    {
                        PropertyInfo = item,
                        StringKey = item.Name,
                        IntKey = hiddenIntKey++
                    };
                    stringMembers.Add(member.StringKey, member);
                }
            }
            else
            {
                var hiddenIntKey = 0;
                foreach (var item in caseInfo.GetFields())
                {

                    var member = new EmittableMember
                    {
                        PropertyInfo = item,
                        IntKey = hiddenIntKey++
                    };
                    intMembers.Add(member.IntKey, member);
                }
            }

            MethodInfo method;
            var methodParameters = new List<EmittableMember>();

            if (caseInfo.GetFields().Any())
            {
                method = ti.GetMethod("New" + caseInfo.Name, BindingFlags.Static | BindingFlags.Public);
                if (method == null) throw new MessagePackDynamicUnionResolverException("can't find public method. case:" + caseInfo.Name);

                var methodLookupDictionary = stringMembers.ToLookup(x => x.Key, x => x, StringComparer.OrdinalIgnoreCase);

                var methodParamIndex = 0;
                foreach (var item in method.GetParameters())
                {
                    EmittableMember paramMember;
                    if (isIntKey)
                    {
                        if (intMembers.TryGetValue(methodParamIndex, out paramMember))
                        {
                            if (item.ParameterType == paramMember.Type)
                            {
                                methodParameters.Add(paramMember);
                            }
                            else
                            {
                                throw new MessagePackDynamicUnionResolverException("can't find matched method parameter, parameterType mismatch. case:" + caseInfo.Name + " parameterIndex:" + methodParamIndex + " paramterType:" + item.ParameterType.Name);
                            }
                        }
                        else
                        {
                            throw new MessagePackDynamicUnionResolverException("can't find matched method parameter, index not found. case:" + caseInfo.Name + " parameterIndex:" + methodParamIndex);
                        }
                    }
                    else
                    {
                        var hasKey = methodLookupDictionary[item.Name];
                        var len = hasKey.Count();
                        if (len != 0)
                        {
                            if (len != 1)
                            {
                                throw new MessagePackDynamicUnionResolverException("duplicate matched method parameter name:" + caseInfo.Name + " parameterName:" + item.Name + " paramterType:" + item.ParameterType.Name);
                            }

                            paramMember = hasKey.First().Value;
                            if (item.ParameterType == paramMember.Type)
                            {
                                methodParameters.Add(paramMember);
                            }
                            else
                            {
                                throw new MessagePackDynamicUnionResolverException("can't find matched method parameter, parameterType mismatch. case:" + caseInfo.Name + " parameterName:" + item.Name + " paramterType:" + item.ParameterType.Name);
                            }
                        }
                        else
                        {
                            throw new MessagePackDynamicUnionResolverException("can't find matched method parameter, index not found. case:" + caseInfo.Name + " parameterName:" + item.Name);
                        }
                    }
                    methodParamIndex++;
                }
            }
            else
            {
                method = ti.GetProperty(caseInfo.Name, BindingFlags.Public | BindingFlags.Static).GetGetMethod();
            }

            return new UnionSerializationInfo
            {
                IsClass = isClass,
                NewMethod = method,
                MethodParameters = methodParameters.ToArray(),
                IsIntKey = isIntKey,
                Members = (isIntKey) ? intMembers.Values.ToArray() : stringMembers.Values.ToArray()
            };
        }

        public class EmittableMember
        {
            public int IntKey { get; set; }
            public string StringKey { get; set; }
            public Type Type { get { return PropertyInfo.PropertyType; } }
            public PropertyInfo PropertyInfo { get; set; }
            public bool IsValueType
            {
                get
                {
                    return ((MemberInfo)PropertyInfo).DeclaringType.GetTypeInfo().IsValueType;
                }
            }

            public void EmitLoadValue(ILGenerator il)
            {
                il.EmitCall(PropertyInfo.GetGetMethod());
            }
        }
    }

    internal class MessagePackDynamicUnionResolverException : Exception
    {
        public MessagePackDynamicUnionResolverException(string message)
            : base(message)
        {

        }
    }
}
