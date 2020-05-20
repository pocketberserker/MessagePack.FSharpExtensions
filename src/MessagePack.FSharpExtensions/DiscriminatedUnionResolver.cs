// Copyright (c) 2017 Yoshifumi Kawai and contributors

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
using System.Buffers;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using MessagePack.Formatters;
using MessagePack.Internal;
using MessagePack.FSharp.Internal;
using Microsoft.FSharp.Reflection;

namespace MessagePack.FSharp
{
    public sealed class DynamicUnionResolver : IFormatterResolver
    {
        public static readonly DynamicUnionResolver Instance;

        const string ModuleName = "MessagePack.FSharp.DynamicUnionResolver";

        static readonly DynamicAssembly DynamicAssembly;

        static readonly Regex SubtractFullNameRegex = new Regex(@", Version=\d+.\d+.\d+.\d+, Culture=\w+, PublicKeyToken=\w+", RegexOptions.Compiled);

        static int nameSequence = 0;

        private DynamicUnionResolver() { }

        static DynamicUnionResolver()
        {
            Instance = new DynamicUnionResolver();
            DynamicAssembly = new DynamicAssembly(ModuleName);
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                if (!FSharpType.IsUnion(typeof(T), null))
                {
                    return;
                }

                var ti = typeof(T).GetTypeInfo();

                var formatterTypeInfo = BuildType(typeof(T));
                if (formatterTypeInfo == null)
                {
                    return;
                }

                Formatter = (IMessagePackFormatter<T>)Activator.CreateInstance(formatterTypeInfo.AsType());
            }
        }

        static TypeInfo BuildType(Type type)
        {
            var ti = type.GetTypeInfo();
            // order by key(important for use jump-table of switch)
            var unionCases = FSharpType.GetUnionCases(type, null).OrderBy(x => x.Tag).ToArray();

            var formatterType = typeof(IMessagePackFormatter<>).MakeGenericType(type);
            TypeBuilder typeBuilder = DynamicAssembly.DefineType("MessagePack.FSharp.Formatters." + SubtractFullNameRegex.Replace(type.FullName, string.Empty).Replace(".", "_") + "Formatter" + +Interlocked.Increment(ref nameSequence), TypeAttributes.Public | TypeAttributes.Sealed, null, new[] { formatterType });

            var stringByteKeysFields = new FieldBuilder[unionCases.Length];

            // create map dictionary
            {
                var method = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);

                foreach(var unionCase in unionCases)
                {
                  stringByteKeysFields[unionCase.Tag] = typeBuilder.DefineField("stringByteKeysField" + unionCase.Tag, typeof(byte[][]), FieldAttributes.Private | FieldAttributes.InitOnly);
                }

                var il = method.GetILGenerator();
                BuildConstructor(type, unionCases, method, stringByteKeysFields, il);
            }

            {
                var method = typeBuilder.DefineMethod(
                    "Serialize",
                    MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                    returnType: null,
                    parameterTypes: new Type[] { typeof(MessagePackWriter).MakeByRefType(), type, typeof(MessagePackSerializerOptions) });
                method.DefineParameter(1, ParameterAttributes.None, "writer");
                method.DefineParameter(2, ParameterAttributes.None, "value");
                method.DefineParameter(3, ParameterAttributes.None, "options");

                var il = method.GetILGenerator();
                BuildSerialize(type, unionCases, method, stringByteKeysFields, il, 1);
            }
            {
                MethodBuilder method = typeBuilder.DefineMethod(
                    "Deserialize",
                    MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                    type,
                    new Type[] { refMessagePackReader, typeof(MessagePackSerializerOptions) });
                method.DefineParameter(1, ParameterAttributes.None, "reader");
                method.DefineParameter(2, ParameterAttributes.None, "options");

                var il = method.GetILGenerator();
                BuildDeserialize(
                    type,
                    unionCases,
                    method,
                    stringByteKeysFields,
                    il,
                    1); // firstArgIndex:0 is this.
            }

            return typeBuilder.CreateTypeInfo();
        }

        static void BuildConstructor(Type type, UnionCaseInfo[] infos, ConstructorInfo method, FieldBuilder[] stringByteKeysFields, ILGenerator il)
        {
            il.EmitLdarg(0);
            il.Emit(OpCodes.Call, objectCtor);

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
                        il.EmitCall(CodeGenHelpersTypeInfo.GetEncodedStringBytes);
                        il.Emit(OpCodes.Stelem_Ref);
                        index++;
                    }

                    il.Emit(OpCodes.Stfld, stringByteKeysFields[info.Tag]);
                }
            }

            il.Emit(OpCodes.Ret);
        }


        // void Serialize(ref [arg:1]MessagePackWriter writer, [arg:2]T value, [arg:3]MessagePackSerializerOptions options);
        static void BuildSerialize(
            Type type,
            UnionCaseInfo[] infos,
            MethodBuilder method,
            FieldBuilder[] stringByteKeysFields,
            ILGenerator il,
            int firstArgIndex
        )
        {
            var tag = getTag(type);
            var ti = type.GetTypeInfo();

            var argWriter = new ArgumentField(il, firstArgIndex);
            var argValue = new ArgumentField(il, firstArgIndex + 1, type);
            var argOptions = new ArgumentField(il, firstArgIndex + 2);

            // if(value == null) return WriteNil
            if (ti.IsClass)
            {
                Label elseBody = il.DefineLabel();

                argValue.EmitLoad();
                il.Emit(OpCodes.Brtrue_S, elseBody);
                argWriter.EmitLoad();
                il.EmitCall(MessagePackWriterTypeInfo.WriteNil);
                il.Emit(OpCodes.Ret);

                il.MarkLabel(elseBody);
            }

            // IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
            if (ti.ImplementedInterfaces.Any(x => x == typeof(IMessagePackSerializationCallbackReceiver)))
            {
                // call directly
                MethodInfo[] runtimeMethods = type.GetRuntimeMethods().Where(x => x.Name == "OnBeforeSerialize").ToArray();
                if (runtimeMethods.Length == 1)
                {
                    argValue.EmitLoad();
                    il.Emit(OpCodes.Call, runtimeMethods[0]); // don't use EmitCall helper(must use 'Call')
                }
                else
                {
                    argValue.EmitLdarg(); // force ldarg
                    il.EmitBoxOrDoNothing(type);
                    il.EmitCall(onBeforeSerialize);
                }
            }

            // IFormatterResolver resolver = options.Resolver;
            LocalBuilder localResolver = il.DeclareLocal(typeof(IFormatterResolver));
            argOptions.EmitLoad();
            il.EmitCall(getResolverFromOptions);
            il.EmitStloc(localResolver);

            // writer.WriteArrayHeader(2, false);
            argWriter.EmitLdarg();
            il.EmitLdc_I4(2);
            il.EmitCall(MessagePackWriterTypeInfo.WriteArrayHeader);

            // writer.Write(value.Tag)
            argWriter.EmitLdarg();
            if (ti.IsClass)
            {
                argValue.EmitLdarg();
            }
            else
            {
                argValue.EmitLdarga();
            }
            il.EmitCall(tag);
            il.EmitCall(MessagePackWriterTypeInfo.WriteInt32);

            var loopEnd = il.DefineLabel();

            // switch-case (offset += resolver.GetFormatter.Serialize(with cast)
            var switchLabels = infos.Select(x => new { Label = il.DefineLabel(), Info = x }).ToArray();
            if (ti.IsClass)
            {
                argValue.EmitLdarg();
            }
            else
            {
                argValue.EmitLdarga();
            }
            il.EmitCall(tag);
            il.Emit(OpCodes.Switch, switchLabels.Select(x => x.Label).ToArray());
            il.Emit(OpCodes.Br, loopEnd); // default

            foreach (var item in switchLabels)
            {
                il.MarkLabel(item.Label);
                EmitSerializeUnionCase(
                    il,
                    type,
                    ti,
                    UnionSerializationInfo.CreateOrNull(type, item.Info),
                    stringByteKeysFields[item.Info.Tag],
                    argWriter,
                    argValue,
                    argOptions,
                    localResolver
                );
                il.Emit(OpCodes.Br, loopEnd);
            }

            // return;
            il.MarkLabel(loopEnd);
            il.Emit(OpCodes.Ret);
        }

        static void EmitSerializeUnionCase(
            ILGenerator il,
            Type type,
            TypeInfo ti,
            UnionSerializationInfo info,
            FieldBuilder stringByteKeysField,
            ArgumentField argWriter,
            ArgumentField argValue,
            ArgumentField argOptions,
            LocalBuilder localResolver
        )
        {
            if (info.IsIntKey)
            {
                // use Array
                var maxKey = info.Members.Select(x => x.IntKey).DefaultIfEmpty(-1).Max();
                var intKeyMap = info.Members.ToDictionary(x => x.IntKey);

                var len = maxKey + 1;
                argWriter.EmitLoad();
                il.EmitLdc_I4(len);
                il.EmitCall(MessagePackWriterTypeInfo.WriteArrayHeader);

                var index = 0;
                for (int i = 0; i <= maxKey; i++)
                {
                    UnionSerializationInfo.EmittableMember member;
                    if (intKeyMap.TryGetValue(i, out member))
                    {
                        EmitSerializeValue(il, ti, member, index++, argWriter, argValue, argOptions, localResolver);
                    }
                    else
                    {
                        // Write Nil as Blanc
                        argWriter.EmitLoad();
                        il.EmitCall(MessagePackWriterTypeInfo.WriteNil);
                    }
                }
            }
            else
            {
                // use Map
                var writeCount = info.Members.Count();

                argWriter.EmitLoad();
                il.EmitLdc_I4(writeCount);
                ////if (writeCount <= MessagePackRange.MaxFixMapCount)
                ////{
                ////    il.EmitCall(MessagePackWriterTypeInfo.WriteFixedMapHeaderUnsafe);
                ////}
                ////else
                {
                    il.EmitCall(MessagePackWriterTypeInfo.WriteMapHeader);
                }

                var index = 0;
                foreach (UnionSerializationInfo.EmittableMember item in info.Members)
                {
                    argWriter.EmitLoad();
                    il.EmitLoadThis();
                    il.EmitLdfld(stringByteKeysField);
                    il.EmitLdc_I4(index);
                    il.Emit(OpCodes.Ldelem_Ref);
                    il.Emit(OpCodes.Call, ReadOnlySpanFromByteArray); // convert byte[] to ReadOnlySpan<byte>

                    // Optimize, WriteRaw(Unity, large) or UnsafeMemory32/64.WriteRawX
                    var valueLen = CodeGenHelpers.GetEncodedStringBytes(item.StringKey).Length;
                    if (valueLen <= MessagePackRange.MaxFixStringLength)
                    {
                        if (UnsafeMemory.Is32Bit)
                        {
                            il.EmitCall(typeof(UnsafeMemory32).GetRuntimeMethod("WriteRaw" + valueLen, new[] { typeof(MessagePackWriter).MakeByRefType(), typeof(ReadOnlySpan<byte>) }));
                        }
                        else
                        {
                            il.EmitCall(typeof(UnsafeMemory64).GetRuntimeMethod("WriteRaw" + valueLen, new[] { typeof(MessagePackWriter).MakeByRefType(), typeof(ReadOnlySpan<byte>) }));
                        }
                    }
                    else
                    {
                        il.EmitCall(MessagePackWriterTypeInfo.WriteRaw);
                    }

                    EmitSerializeValue(il, type.GetTypeInfo(), item, index, argWriter, argValue, argOptions, localResolver);
                    index++;
                }
            }
        }

        static void EmitSerializeValue(
            ILGenerator il,
            TypeInfo type,
            UnionSerializationInfo.EmittableMember member,
            int index,
            ArgumentField argWriter,
            ArgumentField argValue,
            ArgumentField argOptions,
            LocalBuilder localResolver
        )
        {
            Label endLabel = il.DefineLabel();
            Type t = member.Type;
            if (IsOptimizeTargetType(t))
            {
                if (!t.GetTypeInfo().IsValueType)
                {
                    // As a nullable type (e.g. byte[] and string) we need to call WriteNil for null values.
                    Label writeNonNilValueLabel = il.DefineLabel();
                    LocalBuilder memberValue = il.DeclareLocal(t);
                    argValue.EmitLoad();
                    member.EmitLoadValue(il);
                    il.Emit(OpCodes.Dup);
                    il.EmitStloc(memberValue);
                    il.Emit(OpCodes.Brtrue, writeNonNilValueLabel);
                    argWriter.EmitLoad();
                    il.EmitCall(MessagePackWriterTypeInfo.WriteNil);
                    il.Emit(OpCodes.Br, endLabel);

                    il.MarkLabel(writeNonNilValueLabel);
                    argWriter.EmitLoad();
                    il.EmitLdloc(memberValue);
                }
                else
                {
                    argWriter.EmitLoad();
                    argValue.EmitLoad();
                    member.EmitLoadValue(il);
                }

                if (t == typeof(byte[]))
                {
                    il.EmitCall(ReadOnlySpanFromByteArray);
                    il.EmitCall(MessagePackWriterTypeInfo.WriteBytes);
                }
                else
                {
                    il.EmitCall(typeof(MessagePackWriter).GetRuntimeMethod("Write", new Type[] { t }));
                }
            }
            else
            {
                il.EmitLdloc(localResolver);
                il.Emit(OpCodes.Call, getFormatterWithVerify.MakeGenericMethod(t));

                argWriter.EmitLoad();
                argValue.EmitLoad();
                member.EmitLoadValue(il);
                argOptions.EmitLoad();
                il.EmitCall(getSerialize(t));
            }

            il.MarkLabel(endLabel);
        }

        // T Deserialize([arg:1]ref MessagePackReader reader, [arg:2]MessagePackSerializerOptions options);
        static void BuildDeserialize(
            Type type,
            UnionCaseInfo[] infos,
            MethodBuilder method,
            FieldBuilder[] stringByteKeysFields,
            ILGenerator il,
            int firstArgIndex
        )
        {
            var ti = type.GetTypeInfo();

            var reader = new ArgumentField(il, firstArgIndex, @ref: true);
            var argOptions = new ArgumentField(il, firstArgIndex + 1);

            // if(MessagePackBinary.TryReadNil()) { return null; }
            Label falseLabel = il.DefineLabel();
            reader.EmitLdarg();
            il.EmitCall(MessagePackReaderTypeInfo.TryReadNil);
            il.Emit(OpCodes.Brfalse_S, falseLabel);

            if (ti.IsClass)
            {
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ret);
            }
            else
            {
                il.Emit(OpCodes.Ldstr, "typecode is null, struct not supported");
                il.Emit(OpCodes.Newobj, invalidOperationExceptionConstructor);
                il.Emit(OpCodes.Throw);
            }

            il.MarkLabel(falseLabel);

            // IFormatterResolver resolver = options.Resolver;
            LocalBuilder localResolver = il.DeclareLocal(typeof(IFormatterResolver));
            argOptions.EmitLdarg();
            il.EmitCall(getResolverFromOptions);
            il.EmitStloc(localResolver);

            // read-array header and validate, reader.ReadArrayHeader() != 2) throw;
            Label rightLabel = il.DefineLabel();
            reader.EmitLdarg();
            il.EmitCall(MessagePackReaderTypeInfo.ReadArrayHeader);
            il.EmitLdc_I4(2);
            il.Emit(OpCodes.Beq_S, rightLabel);
            il.Emit(OpCodes.Ldstr, "Invalid Union data was detected. Type:" + type.FullName);
            il.Emit(OpCodes.Newobj, invalidOperationExceptionConstructor);
            il.Emit(OpCodes.Throw);

            il.MarkLabel(rightLabel);

            // read key
            LocalBuilder key = il.DeclareLocal(typeof(int));
            reader.EmitLdarg();
            il.EmitCall(MessagePackReaderTypeInfo.ReadInt32);
            il.EmitStloc(key);

            // switch->read
            LocalBuilder result = il.DeclareLocal(type);
            Label loopEnd = il.DefineLabel();
            if(ti.IsClass)
            {
                il.Emit(OpCodes.Ldnull);
                il.EmitStloc(result);
            }
            il.Emit(OpCodes.Ldloc, key);

            var switchLabels = infos.Select(x => new { Label = il.DefineLabel(), Info = x }).ToArray();
            il.Emit(OpCodes.Switch, switchLabels.Select(x => x.Label).ToArray());

            // default
            reader.EmitLdarg();
            il.EmitCall(MessagePackReaderTypeInfo.Skip);
            il.Emit(OpCodes.Br, loopEnd);

            foreach (var item in switchLabels)
            {
                il.MarkLabel(item.Label);
                EmitDeserializeUnionCase(
                    il,
                    type,
                    UnionSerializationInfo.CreateOrNull(type, item.Info),
                    key,
                    stringByteKeysFields[item.Info.Tag],
                    reader,
                    argOptions,
                    localResolver
                );
                il.Emit(OpCodes.Stloc, result);
                il.Emit(OpCodes.Br, loopEnd);
            }

            il.MarkLabel(loopEnd);

            il.Emit(OpCodes.Ldloc, result);
            il.Emit(OpCodes.Ret);
        }

        static void EmitDeserializeUnionCase(
            ILGenerator il,
            Type type,
            UnionSerializationInfo info,
            LocalBuilder unionKey,
            FieldBuilder stringByteKeysField,
            ArgumentField reader,
            ArgumentField argOptions,
            LocalBuilder localResolver
        )
        {
            // options.Security.DepthStep(ref reader);
            argOptions.EmitLoad();
            il.EmitCall(getSecurityFromOptions);
            reader.EmitLdarg();
            il.EmitCall(securityDepthStep);

            // var length = ReadMapHeader(ref byteSequence);
            LocalBuilder length = il.DeclareLocal(typeof(int)); // [loc:1]
            reader.EmitLdarg();

            if (info.IsIntKey)
            {
                il.EmitCall(MessagePackReaderTypeInfo.ReadArrayHeader);
            }
            else
            {
                il.EmitCall(MessagePackReaderTypeInfo.ReadMapHeader);
            }

            il.EmitStloc(length);

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

                LocalBuilder buffer = il.DeclareLocal(typeof(ReadOnlySpan<byte>));
                LocalBuilder longKey = il.DeclareLocal(typeof(ulong));

                // for (int i = 0; i < len; i++)
                il.EmitIncrementFor(length, forILocal =>
                {
                    Label readNext = il.DefineLabel();
                    Label loopEnd = il.DefineLabel();

                    reader.EmitLdarg();
                    il.EmitCall(ReadStringSpan);
                    il.EmitStloc(buffer);

                    // gen automata name lookup
                    automata.EmitMatch(
                        il,
                        buffer,
                        longKey,
                        x =>
                        {
                            var i = x.Value;
                            if (infoList[i].MemberInfo != null)
                            {
                                EmitDeserializeValue(il, infoList[i], i, reader, argOptions, localResolver);
                                il.Emit(OpCodes.Br, loopEnd);
                            }
                            else
                            {
                                il.Emit(OpCodes.Br, readNext);
                            }
                        },
                        () =>
                        {
                            il.Emit(OpCodes.Br, readNext);
                        });

                    il.MarkLabel(readNext);
                    reader.EmitLdarg();
                    il.EmitCall(MessagePackReaderTypeInfo.Skip);

                    il.MarkLabel(loopEnd);
                });
            }
            else
            {
                LocalBuilder key = il.DeclareLocal(typeof(int));
                Label switchDefault = il.DefineLabel();

                il.EmitIncrementFor(length, forILocal =>
                {
                    Label loopEnd = il.DefineLabel();

                    il.EmitLdloc(forILocal);
                    il.EmitStloc(key);

                    // switch... local = Deserialize
                    il.EmitLdloc(key);

                    il.Emit(OpCodes.Switch, infoList.Select(x => x.SwitchLabel).ToArray());

                    il.MarkLabel(switchDefault);

                    // default, only read. reader.ReadNextBlock();
                    reader.EmitLdarg();
                    il.EmitCall(MessagePackReaderTypeInfo.Skip);
                    il.Emit(OpCodes.Br, loopEnd);

                    if (gotoDefault != null)
                    {
                        il.MarkLabel(gotoDefault.Value);
                        il.Emit(OpCodes.Br, switchDefault);
                    }

                    var i = 0;
                    foreach (DeserializeInfo item in infoList)
                    {
                        if (item.MemberInfo != null)
                        {
                            il.MarkLabel(item.SwitchLabel);
                            EmitDeserializeValue(il, item, i++, reader, argOptions, localResolver);
                            il.Emit(OpCodes.Br, loopEnd);
                        }
                    }

                    il.MarkLabel(loopEnd);
                });
            }

            // create result union case
            LocalBuilder structLocal = EmitNewObject(il, type, info, infoList);

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

            // reader.Depth--;
            reader.EmitLdarg();
            il.Emit(OpCodes.Dup);
            il.EmitCall(readerDepthGet);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Sub_Ovf);
            il.EmitCall(readerDepthSet);

            if (info.IsStruct)
            {
                il.Emit(OpCodes.Ldloc, structLocal);
            }
        }

        static void EmitDeserializeValue(
            ILGenerator il,
            DeserializeInfo info,
            int index,
            ArgumentField argReader,
            ArgumentField argOptions,
            LocalBuilder localResolver
        )
        {
            Label storeLabel = il.DefineLabel();
            UnionSerializationInfo.EmittableMember member = info.MemberInfo;
            Type t = member.Type;
            if (IsOptimizeTargetType(t))
            {
                if (!t.GetTypeInfo().IsValueType)
                {
                    // As a nullable type (e.g. byte[] and string) we need to first call TryReadNil
                    // if (reader.TryReadNil())
                    Label readNonNilValueLabel = il.DefineLabel();
                    argReader.EmitLdarg();
                    il.EmitCall(MessagePackReaderTypeInfo.TryReadNil);
                    il.Emit(OpCodes.Brfalse_S, readNonNilValueLabel);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Br, storeLabel);

                    il.MarkLabel(readNonNilValueLabel);
                }

                argReader.EmitLdarg();
                if (t == typeof(byte[]))
                {
                    LocalBuilder local = il.DeclareLocal(typeof(ReadOnlySequence<byte>?));
                    il.EmitCall(MessagePackReaderTypeInfo.ReadBytes);
                    il.EmitStloc(local);
                    il.EmitLdloca(local);
                    il.EmitCall(ArrayFromNullableReadOnlySequence);
                }
                else
                {
                    il.EmitCall(MessagePackReaderTypeInfo.TypeInfo.GetDeclaredMethods("Read" + t.Name).First(x => x.GetParameters().Length == 0));
                }
            }
            else
            {
                il.EmitLdloc(localResolver);
                il.EmitCall(getFormatterWithVerify.MakeGenericMethod(t));
                argReader.EmitLdarg();
                argOptions.EmitLoad();
                il.EmitCall(getDeserialize(t));
            }

            il.MarkLabel(storeLabel);
            il.EmitStloc(info.LocalField);
        }

        private static LocalBuilder EmitNewObject(ILGenerator il, Type type, UnionSerializationInfo info, DeserializeInfo[] members)
        {
            if (info.IsClass)
            {
                foreach (UnionSerializationInfo.EmittableMember item in info.MethodParameters)
                {
                    DeserializeInfo local = members.First(x => x.MemberInfo == item);
                    il.EmitLdloc(local.LocalField);
                }

                il.Emit(OpCodes.Call, info.NewMethod);

                return null;
            }
            else
            {
                LocalBuilder result = il.DeclareLocal(type);
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

        private static bool IsOptimizeTargetType(Type type)
        {
            return type == typeof(Int16)
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
                || type == typeof(byte[])

            // Do not include types that resolvers are allowed to modify.
            ////|| type == typeof(DateTime) // OldSpec has no support, so for that and perf reasons a .NET native DateTime resolver exists.
            ////|| type == typeof(string) // https://github.com/Cysharp/MasterMemory provides custom formatter for string interning.
            ;
        }

        // EmitInfos...
        private static readonly Type refMessagePackReader = typeof(MessagePackReader).MakeByRefType();

        private static readonly MethodInfo ReadOnlySpanFromByteArray = typeof(ReadOnlySpan<byte>).GetRuntimeMethod("op_Implicit", new[] { typeof(byte[]) });
        private static readonly MethodInfo ReadStringSpan = typeof(CodeGenHelpers).GetRuntimeMethod(nameof(CodeGenHelpers.ReadStringSpan), new[] { typeof(MessagePackReader).MakeByRefType() });
        private static readonly MethodInfo ArrayFromNullableReadOnlySequence = typeof(CodeGenHelpers).GetRuntimeMethod(nameof(CodeGenHelpers.GetArrayFromNullableSequence), new[] { typeof(ReadOnlySequence<byte>?).MakeByRefType() });

        private static readonly MethodInfo getFormatterWithVerify = typeof(FormatterResolverExtensions).GetRuntimeMethods().First(x => x.Name == nameof(FormatterResolverExtensions.GetFormatterWithVerify));
        private static readonly MethodInfo getResolverFromOptions = typeof(MessagePackSerializerOptions).GetRuntimeProperty(nameof(MessagePackSerializerOptions.Resolver)).GetMethod;
        private static readonly MethodInfo getSecurityFromOptions = typeof(MessagePackSerializerOptions).GetRuntimeProperty(nameof(MessagePackSerializerOptions.Security)).GetMethod;
        private static readonly MethodInfo securityDepthStep = typeof(MessagePackSecurity).GetRuntimeMethod(nameof(MessagePackSecurity.DepthStep), new[] { typeof(MessagePackReader).MakeByRefType() });
        private static readonly MethodInfo readerDepthGet = typeof(MessagePackReader).GetRuntimeProperty(nameof(MessagePackReader.Depth)).GetMethod;
        private static readonly MethodInfo readerDepthSet = typeof(MessagePackReader).GetRuntimeProperty(nameof(MessagePackReader.Depth)).SetMethod;
        private static readonly Func<Type, MethodInfo> getSerialize = t => typeof(IMessagePackFormatter<>).MakeGenericType(t).GetRuntimeMethod(nameof(IMessagePackFormatter<int>.Serialize), new[] { typeof(MessagePackWriter).MakeByRefType(), t, typeof(MessagePackSerializerOptions) });
        private static readonly Func<Type, MethodInfo> getDeserialize = t => typeof(IMessagePackFormatter<>).MakeGenericType(t).GetRuntimeMethod(nameof(IMessagePackFormatter<int>.Deserialize), new[] { refMessagePackReader, typeof(MessagePackSerializerOptions) });
        //// static readonly ConstructorInfo dictionaryConstructor = typeof(ByteArrayStringHashTable).GetTypeInfo().DeclaredConstructors.First(x => { var p = x.GetParameters(); return p.Length == 1 && p[0].ParameterType == typeof(int); });
        //// static readonly MethodInfo dictionaryAdd = typeof(ByteArrayStringHashTable).GetRuntimeMethod("Add", new[] { typeof(string), typeof(int) });
        //// static readonly MethodInfo dictionaryTryGetValue = typeof(ByteArrayStringHashTable).GetRuntimeMethod("TryGetValue", new[] { typeof(ArraySegment<byte>), refInt });
        private static readonly ConstructorInfo messagePackSerializationExceptionMessageOnlyConstructor = typeof(MessagePackSerializationException).GetTypeInfo().DeclaredConstructors.First(x =>
        {
            ParameterInfo[] p = x.GetParameters();
            return p.Length == 1 && p[0].ParameterType == typeof(string);
        });

        static readonly ConstructorInfo invalidOperationExceptionConstructor = typeof(System.InvalidOperationException).GetTypeInfo().DeclaredConstructors.First(x => { var p = x.GetParameters(); return p.Length == 1 && p[0].ParameterType == typeof(string); });
        static readonly Type refUnionCaseInfo = typeof(Microsoft.FSharp.Reflection.UnionCaseInfo).MakeByRefType();

        static readonly ConstructorInfo objectCtor = typeof(object).GetTypeInfo().DeclaredConstructors.First(x => x.GetParameters().Length == 0);

        static readonly Func<Type, MethodInfo> getTag = type => type.GetTypeInfo().GetProperty("Tag").GetGetMethod();

        static readonly MethodInfo onBeforeSerialize = typeof(IMessagePackSerializationCallbackReceiver).GetRuntimeMethod("OnBeforeSerialize", Type.EmptyTypes);
        static readonly MethodInfo onAfterDeserialize = typeof(IMessagePackSerializationCallbackReceiver).GetRuntimeMethod("OnAfterDeserialize", Type.EmptyTypes);

        internal static class MessagePackWriterTypeInfo
        {
            internal static readonly TypeInfo TypeInfo = typeof(MessagePackWriter).GetTypeInfo();

            internal static readonly MethodInfo WriteMapHeader = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.WriteMapHeader), new[] { typeof(int) });
            internal static readonly MethodInfo WriteArrayHeader = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.WriteArrayHeader), new[] { typeof(int) });
            internal static readonly MethodInfo WriteBytes = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.Write), new[] { typeof(ReadOnlySpan<byte>) });
            internal static readonly MethodInfo WriteNil = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.WriteNil), Type.EmptyTypes);
            internal static readonly MethodInfo WriteRaw = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.WriteRaw), new[] { typeof(ReadOnlySpan<byte>) });
            internal static readonly MethodInfo WriteInt32 = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.Write), new[] { typeof(int) });
        }

        internal static class MessagePackReaderTypeInfo
        {
            internal static readonly TypeInfo TypeInfo = typeof(MessagePackReader).GetTypeInfo();

            internal static readonly MethodInfo ReadArrayHeader = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.ReadArrayHeader), Type.EmptyTypes);
            internal static readonly MethodInfo ReadMapHeader = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.ReadMapHeader), Type.EmptyTypes);
            internal static readonly MethodInfo ReadBytes = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.ReadBytes), Type.EmptyTypes);
            internal static readonly MethodInfo TryReadNil = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.TryReadNil), Type.EmptyTypes);
            internal static readonly MethodInfo Skip = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.Skip), Type.EmptyTypes);
            internal static readonly MethodInfo ReadInt32 = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.ReadInt32), Type.EmptyTypes);
        }

        internal static class CodeGenHelpersTypeInfo
        {
            public static readonly MethodInfo GetEncodedStringBytes = typeof(CodeGenHelpers).GetRuntimeMethod(nameof(CodeGenHelpers.GetEncodedStringBytes), new[] { typeof(string) });
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

        public static UnionSerializationInfo CreateOrNull(Type type, UnionCaseInfo caseInfo)
        {
            type = caseInfo.DeclaringType;

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
