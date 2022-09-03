using System;
using System.Collections.Generic;
using BagoumLib.Reflection;
using FluentIL;
using NUnit.Framework;
using static BagoumLib.Reflection.BuilderHelpers;

namespace Tests.BagoumLib.Reflection {
public class TypeConstruction {
    private static readonly Type tf = typeof(float);
    private static readonly Type ti = typeof(int);

    [SetUp]
    public void Setup() {
        DebugOutput.Output = new ConsoleOutput();
    }
    
    [Test]
    public void TestValue() {
        var builder = new CustomDataBuilder("Testing", null, tf, ti);
        builder.GetVariableKey("ffff", typeof(float));
        var req1 = new CustomDataDescriptor(
            new("f1", tf) { MakeProperty = true },
            new("f2", tf),
            new("f3", tf),
            new("i1", ti)
        );
        var myType1 = builder.CreateCustomDataType(req1, out _);
        Assert.AreSame(myType1, builder.CreateCustomDataType(req1, out _));
        dynamic bobj = Activator.CreateInstance(builder.CustomDataBaseType)!;
        dynamic obj = Activator.CreateInstance(myType1)!;
        obj.m0_f1 = 5f;
        obj.WriteFloat(builder.GetVariableKey("f2", tf), 4f);
        obj.m1_i1 = 1;
        obj.P0_f1 += obj.m0_f2;
        var obj2 = obj.Clone();
        Assert.AreEqual(obj2.m0_f1, 9f);
        Assert.AreEqual(obj.ReadFloat(builder.GetVariableKey("f1", tf)), 9f);
        Assert.AreEqual(obj2.ReadInt(builder.GetVariableKey("i1", ti)), 1);
        Assert.AreEqual(Assert.Throws<KeyNotFoundException>(() => obj.ReadFloat(6)).Message, "Custom data object does not have a float to get with ID 6");
    }
    
    [Test]
    public void TestSubclass() {
        var builder = new CustomDataBuilder("Testing", null, tf, ti);
        var rBase = new CustomDataDescriptor(
            new("f1", tf),
            new("f2", tf)
        );
        var tBase = builder.CreateCustomDataType(rBase, out _);
        Assert.AreEqual(Assert.Throws<Exception>(() =>
            builder.CreateCustomDataType(new(new("f1", tf), new("f3", tf)) {BaseType = tBase }, out _)).Message, "f1<float>;f3<float> is not a subclass of f1<float>;f2<float>");

        var rDeriv = new CustomDataDescriptor(
            new("f1", tf),
            new("f2", tf),
            new("f3", tf)
        );
        var tNorm = builder.CreateCustomDataType(rDeriv, out _);
        var tDeriv = builder.CreateCustomDataType(rDeriv with {BaseType = tBase}, out _);
        Assert.AreNotEqual(tNorm, tDeriv);
        
        dynamic norm = Activator.CreateInstance(tNorm)!;
        dynamic deriv = Activator.CreateInstance(tDeriv)!;
        deriv.m0_f3 = 6;
        Assert.AreEqual(deriv.ReadFloat(builder.GetVariableKey("f3", tf)), 6f);
        Assert.IsTrue(deriv.GetType().IsSubclassOf(tBase));
        Assert.IsFalse(norm.GetType().IsSubclassOf(tBase));

        int k = 5;
    }

    [Test]
    public void TestCustomBaseClass() {
        var builder = new CustomDataBuilder(typeof(MyBaseClass), "Testing", null, tf);

        var tBase = builder.CreateCustomDataType(new(new("f1", tf), new("f2", tf)), out _);
        var tDeriv = builder.CreateCustomDataType(new(new("f1", tf), new("f2", tf), new("f3", tf)) { BaseType = tBase }, out _);

        dynamic bobj = Activator.CreateInstance(tBase)!;
        dynamic obj = Activator.CreateInstance(tDeriv)!;
        Assert.IsTrue(obj is MyBaseClass);
        Assert.AreEqual(obj.myString, "hello");
        obj.myString = "world";
        obj.m0_f1 = 1f;
        obj.m0_f3 = 3f;
        dynamic obj2 = (obj as MyBaseClass)!.Clone();
        Assert.AreEqual(obj2.myString, "world");
        Assert.AreEqual(obj2.m0_f1, 1f);
        Assert.AreEqual(obj2.m0_f3, 3f);
        dynamic obj3 = Activator.CreateInstance(tDeriv)!;
        (obj as MyBaseClass)!.CopyInto(obj3);
        Assert.AreEqual(obj3.myString, "world");
        Assert.AreEqual(obj3.m0_f1, 0f);
        obj3.myString = "foo";
        (obj as MyBaseClass)!.CopyIntoVirtual(obj3);
        Assert.AreEqual(obj3.myString, "world");
        Assert.AreEqual(obj3.m0_f1, 1f);
        Assert.AreEqual(obj3.m0_f3, 3f);
        Assert.IsFalse(bobj.HasFloat(builder.GetVariableKey("f3", typeof(float))));
        Assert.IsTrue((obj as MyBaseClass)!.HasFloat(builder.GetVariableKey("f3", typeof(float))));
        obj3.myString = "foo";

    }

    public class MyBaseClass {
        public string myString = "hello";

        public MyBaseClass CopyInto(MyBaseClass copyee) {
            copyee.myString = myString;
            return copyee;
        }

        public virtual MyBaseClass CopyIntoVirtual(MyBaseClass copyee) => CopyInto(copyee);

        public virtual MyBaseClass Clone() => CopyInto(new MyBaseClass());

        public virtual bool HasFloat(int id) => false;
        public virtual float ReadFloat(int id) => throw new Exception();
        public virtual float WriteFloat(int id, float val) => throw new Exception();
    }
}
}