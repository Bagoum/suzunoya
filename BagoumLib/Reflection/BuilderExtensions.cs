using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BagoumLib.Functional;
using FluentIL;
using JetBrains.Annotations;
using static BagoumLib.Reflection.BuilderHelpers;

namespace BagoumLib.Reflection {
public delegate void Emitter(IEmitter il);
[PublicAPI]
public static class BuilderExtensions {
    public static readonly ConstructorInfo keyNotFound =
        typeof(KeyNotFoundException).GetConstructor(new[] { typeof(string) })!;

    private static readonly ConstructorInfo objectConstr = typeof(object).GetConstructor(Type.EmptyTypes)!;
    public static IConstructorBuilder MakeEmptyConstructor(this ITypeBuilder tb, ConstructorInfo? baseConstr = null) {
        var cons = tb.NewConstructor();
        cons.SetMethodAttributes(MethodAttributes.Public)
            .Body()
            .LdArg0()
            .Call(baseConstr ?? objectConstr)
            .Ret();
        return cons;
    }
    public static IPropertyBuilder AddProperty(this ITypeBuilder tb, string name, Type t, MethodAttributes getSetAttr) {
        var prop = tb.NewProperty(name, t);
        prop.Getter(m => m.MethodAttributes(getSetAttr));
        prop.Setter(m => m.MethodAttributes(getSetAttr));
        return prop;
    }
    
    //https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.typebuilder?view=net-6.0
    public static IPropertyBuilder AddAutoProperty(this ITypeBuilder tb, IFieldBuilder field, string propName, Type t) =>
        tb.AddProperty(propName, t, ImplementedProperty)
            .Getter(m => m.Body()
                .LdArg0()
                .LdFld(field)
                .Ret())
            .Setter(m => m.Body()
                .LdArg0()
                .LdArg1()
                .StFld(field)
                .Ret());
    
    public static IPropertyBuilder AddAutoProperty(this ITypeBuilder tb, string fieldName, string propName, Type t) =>
        AddAutoProperty(tb, tb.NewField(fieldName, t).Attributes(FieldAttributes.Public), propName, t);
    
    
    /// <summary>
    /// Create a jumptable switch statement.
    /// </summary>
    /// <param name="il">IL generator</param>
    /// <param name="arg">Argument to switch</param>
    /// <param name="_cases">Cases for the switch statement. Keys may be negative.</param>
    /// <param name="deflt">Default case</param>
    /// <param name="isReturnSwitch">True iff all switch cases (including default) end in Ret.</param>
    /// <returns></returns>
    public static IEmitter EmitSwitch(this IEmitter il,
        Emitter arg, IEnumerable<(int key, Emitter emitter)> _cases, Emitter deflt, bool isReturnSwitch = false) {
        var cases = _cases.OrderBy(x => x.key).ToList();
        if (cases.Count == 0) {
            deflt(il);
            return il;
        }
        il.DefineLabel(out var defaultCase).DefineLabel(out var endOfSwitch);
        var minKey = cases.Min(x => x.key);
        var maxKey = cases.Max(x => x.key);
        var nCases = maxKey - minKey + 1;
        var jump = new ILabel[nCases];
        for (int ii = 0; ii < jump.Length; ++ii)
            il.DefineLabel(out jump[ii]);
        arg(il);
        il.LdcI4(minKey)
            .Sub()
            .Switch(jump)
            .BrS(defaultCase);
        int li = 0;
        foreach (var (key, emitter) in cases) {
            var relKey = key - minKey;
            if (li > relKey)
                throw new Exception("Misordering in switch emission, are the cases ordered?");
            while (li < relKey) {
                //Failure cases
                il.MarkLabel(jump[li]);
                il.BrS(defaultCase);
                ++li;
            }
            il.MarkLabel(jump[li]);
            emitter(il);
            if (!isReturnSwitch)
                il.BrS(endOfSwitch);
            ++li;
        }
        il.MarkLabel(defaultCase);
        deflt(il);
        if (!isReturnSwitch)
            il.MarkLabel(endOfSwitch);
        return il;
    }

    public static IEmitter EmitThrow(this IEmitter il, string msg) => il
        .LdStr(msg)
        .Newobj(keyNotFound)
        .Throw();

    public static readonly MethodInfo stringConcat =
        typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) }) ?? throw new Exception();
    public static readonly MethodInfo intToString =
        typeof(int).GetMethod("ToString", Array.Empty<Type>()) ?? throw new Exception();
    public static IEmitter EmitThrow(this IEmitter il, string msg, Emitter suffix) {
        il.LdStr(msg);
        suffix(il);
        il
            .Call(stringConcat)
            .Newobj(keyNotFound)
            .Throw();
        return il;
    }
}
}