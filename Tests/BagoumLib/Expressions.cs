using System;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using NUnit.Framework;
using static Tests.AssertHelpers;
using Ex = System.Linq.Expressions.Expression;

namespace Tests.BagoumLib {
public class Expressions {
    private static ParameterExpression Prm<T>(string name) => Ex.Parameter(typeof(T), name);
    private static Expression ExC<T>(T obj) => Ex.Constant(obj, typeof(T));
    
    private static readonly ExpressionPrinter ExPrinter = new();
    private static void AssertEx(Expression e, string expected) => StringsApproxEqual(expected, ExPrinter.Print(e));
    
    private static readonly ParameterExpression x = Prm<int>("x");
    private static readonly ParameterExpression y = Prm<int>("y");
    private static readonly ParameterExpression z = Prm<int>("z");
    private static readonly LabelTarget breaker = Ex.Label("break_me");
    private static readonly LabelTarget continuer = Ex.Label("continue_me");
    private static readonly LabelTarget returnInt = Ex.Label(typeof(int), "return");

    [Test]
    public void TestBinary() {
        var a = Prm<int[]>("arr");
        AssertEx(x.Add(y), @"x + y");
        AssertEx(x.Add(y.Mul(x)), @"x + (y * x)");
        AssertEx(Ex.ArrayIndex(a, x), @"arr[x]");
        AssertEx(Ex.ArrayIndex(a, Ex.SubtractAssign(x, y)), @"arr[x -= y]");
        AssertEx(Ex.ArrayAccess(Prm<int[,]>("arr2"), x, Ex.AddAssign(y, z)), @"arr2[x, y += z]");
        AssertEx(Ex.Assign(x, y), @"x = y");
    }
    
    [Test]
    public void TestAutonaming() {
        //Preferred name already taken => gets reassigned
        var x2 = Prm<int>("x");
        var _1 = Prm<int>(null!);
        var _2 = Prm<Func<float,bool>>(null!);
        var _3 = Prm<string[]>(null!);
        AssertEx(Ex.Block(new[]{x,x2,_1,_2,_3}, x.Add(x2).Add(_1)), @"int x;
int int0;
int int1;
Func<float, bool> funcOfFloatAndBool2;
string[] stringArray3;
(x + int0) + int1;
");
    }
    

    [Test]
    public void TestLoop() {
        var loop = Ex.Block(new[]{x},
            Ex.Assign(x, ExC(1)),
            Ex.Loop(Ex.Block(
                Ex.IfThenElse(x.GT(y),
                    Ex.Break(breaker),
                    Ex.Block(
                        Ex.MultiplyAssign(x, ExC(2)),
                        Ex.Continue(continuer),
                        Ex.PreIncrementAssign(x)))
                ), breaker, continuer), 
            x);
        var func = Expression.Lambda<Func<int, int>>(loop, y).Compile();
        Assert.AreEqual(512, func(509));
        AssertEx(loop, @"int x = 1;
while (true) {
    continue_me:;
    if (x > y) {
        break;
    } else {
        x *= 2;
        continue;
        ++x;
    }
}
break_me:;");
    }

    [Test]
    public void TestReturn() {
        var body = Ex.Block(
            Ex.IfThen(x.GT(ExC(1)), Ex.Return(returnInt, ExC(1))),
            Ex.Return(returnInt, x.Sub(ExC(5))),
            Ex.Label(returnInt, Ex.Default(typeof(int)))
        );
        var func = Expression.Lambda<Func<int, int>>(body, x).Compile();
        Assert.AreEqual(1, func(10));
        Assert.AreEqual(-8, func(-3));
        AssertEx(body, @"if (x > 1) {
    return 1;
}
return x - 5;
return:;");
    }

    [Test]
    public void TestTryCatch() {
        var exc1 = Prm<InvalidOperationException>("ioExc");
        var exc2 = Prm<InvalidCastException>("castExc");
        var tryBlock = Ex.MakeTry(typeof(int), Ex.Throw(Ex.New(typeof(Exception)), typeof(int)), Ex.Assign(x, ExC(5)), null, new[] {
            Ex.Catch(exc1,
                Ex.Block(new[] {z}, Ex.Assign(z, y), y),
                Ex.Equal(Ex.PropertyOrField(exc1, "Message"), ExC("hello"))),
            Ex.Catch(exc2, y)
        });
        AssertEx(tryBlock, @"try {
    throw new Exception();
} catch (InvalidOperationException ioExc) when (ioExc.Message == ""hello"") {
    int z = y;
    return y;
} catch (InvalidCastException castExc) {
    return y;
} finally {
    x = 5;
}");
    }

    
    [Test]
    public void TestLambda() {
        var mul = Expression.Lambda<Func<int, int, int>>(Ex.Block(x.Is(x.Mul(y)), x), x, y);
        var add = Expression.Lambda<Func<int, int, int>>(x.Add(y), x, y);
        var mulThenAdd = Ex.Invoke(add, Ex.Invoke(mul, x, y), z);
        AssertEx(mulThenAdd, @"((Func<int, int, int>) ((int x, int y) => x + y))(((Func<int, int, int>) ((int x, int y) => {
    x = (x * y);
    return x;
}))(x, y), z)");
    }

    delegate void MyDelegate(in int x, ref int y, out int z);
    
    [Test]
    public void TestRefLambdaAndInvoke() {
        var a = Ex.Parameter(typeof(int), "a");
        
        var xr = Ex.Parameter(typeof(int).MakeByRefType(), "x");
        var yr = Ex.Parameter(typeof(int).MakeByRefType(), "y");
        var zr = Ex.Parameter(typeof(int).MakeByRefType(), "z");

        var ex = Ex.Invoke(Ex.Lambda<MyDelegate>(zr.Is(xr.Add(yr)), xr, yr, zr), a, a, a);
        AssertEx(ex, @"
((Expressions.MyDelegate) ((in int x, ref int y, out int z) => z = (x + y)))(in a, ref a, out a)");
    }


    [Test]
    public void TestMethodAndMember() {
        var str = ExC("red");
        var ex = Ex.Property(Ex.Call(str, typeof(string).GetMethod("ToLower", new Type[0])!), "Chars",
            Ex.Add(
                Ex.Call(null, typeof(Math).GetMethod("Sign", new[]{typeof(double)})!,
                    Ex.Field(null, typeof(Math).FieldInfo("PI", false))),
                Ex.Property(str, typeof(string).PropertyInfo("Length"))
            ));
        AssertEx(ex, @"""red"".ToLower()[Math.Sign(Math.PI) + ""red"".Length]");
    }

    [Test]
    public void TestJoinedDeclaration() {
        AssertEx(Ex.Block(new[] {x, y}, x.Is(ExC(5)), y.Is(x.Is(ExC(0)))), @"int x = 5;
int y = (x = 0);");
        AssertEx(Ex.Block(new[] {x, y}, y.Is(x.Is(ExC(0))), x.Is(ExC(5))), @"int x;
int y = (x = 0);
x = 5;");
    }

    [Test]
    public void TestConditional() {
        AssertEx(Ex.Condition(x.GT0(), x.Is(y), x.Is(z), typeof(void)), @"
if (x > default(int)) {
    x = y;
} else {
    x = z;
}");
        AssertEx(Ex.IfThen(x.GT0(), x.Is(y)), @"
if (x > default(int)) {
    x = y;
}");
    }

    [Test]
    public void ObjectPrinterEdgeCases() {
        var p = new CSharpObjectPrinter();
        Assert.AreEqual(p.Print('"'), "'\\\"'");
        Assert.AreEqual(p.Print("hello\"world"), "\"hello\\\"world\"");
        Assert.AreEqual(p.Print("hello\nworld"), "\"hello\\nworld\"");
    }

    [Test]
    public void TypePrinter() {
        var p = new CSharpTypePrinter(){PrintTypeNamespace = _ => true};
        Assert.AreEqual("System.Func<float, float>", p.Print(typeof(Func<float,float>)));
        Assert.AreEqual("float[]", p.Print(typeof(float[])));
    }
}
}