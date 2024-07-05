using System;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using NUnit.Framework;
using static Tests.AssertHelpers;
using Ex = System.Linq.Expressions.Expression;
using static NUnit.Framework.Assert;

namespace Tests.BagoumLib {
public class ExpressionLinearizer {
    private static ParameterExpression Prm<T>(string name) => Ex.Parameter(typeof(T), name);
    private static Expression ExC<T>(T obj) => Ex.Constant(obj, typeof(T));
    
    private static readonly ExpressionPrinter ExPrinter = new();
    private static readonly ExpressionPrinter SafeExPrinter = new() { SafeLinearize = true };
    
        private static float Compile(Ex ex) => Expression.Lambda<Func<float>>(ex).Compile()();
    private static T Compile<T>(Ex ex) => Expression.Lambda<Func<T>>(ex).Compile()();
    private static Func<float,float> Compile1(Ex ex, ParameterExpression pex) => Expression.Lambda<Func<float, float>>(ex, pex).Compile();
    private static ParameterExpression VF(string name) => Ex.Variable(typeof(float), name);
    private static ParameterExpression V<T>(string name) => Ex.Variable(typeof(T), name);

    private static void Prints(Expression ex, string expected) => 
        StringsApproxEqual(expected, ExPrinter.Print(ex));

    private static void LinPrints(Expression ex, string expected) =>
        StringsApproxEqual(expected.Replace("\r\n", "\n"), ExPrinter.LinearizePrint(ex));
    private static void SafeLinPrints(Expression ex, string expected) =>
        StringsApproxEqual(expected.Replace("\r\n", "\n"), SafeExPrinter.LinearizePrint(ex));

    [Test]
    public void TestAssign() {
        var x = VF("x");
        var yi = VF("y");
        var zi = VF("z");

        var ex = x.Is(Ex.Block(new[] {yi},
            yi.Is(ExC(5f)),
            yi.Add(x)
        ));

        //Block on RHS is not directly printable
        Assert.Throws<Exception>(() => ExPrinter.Print(ex));
        LinPrints(ex, @"
float y = 5f;
x = (y + x);");

        AreEqual(11f, Compile1(new LinearizeVisitor().Visit(ex), x)(6));

        var ex2 = Ex.Block(
            x.Is(Ex.Block(new[] {yi},
                yi.Is(ExC(5f)),
                yi.Add(x)
            ).Add(Ex.Block(new[] {zi},
                zi.Is(x),
                zi.Mul(2f)
            ))),
            x.Add(2f)
        );
        
        Assert.Throws<Exception>(() => ExPrinter.Print(ex2));
        LinPrints(ex2, @"
float y = 5f;
float z = x;
x = ((y + x) + (z * 2f));
x + 2f;");
    }

    [Test]
    public void TestCond() {
        var x = VF("x");
        var yi = VF("y");
        var zi = VF("z");
        var wi = VF("w");
        var ex = wi.Is(Ex.Condition(Ex.Block(zi.Is(ExC(5f)), zi.Add(x).GT(ExC(5f))),
            Ex.Block(new[] {yi},
                yi.Is(ExC(5f)),
                yi.Add(x)),
            ExC(2f)
        ));
        LinPrints(ex, @"
float flatTernary0;
z = 5f;
if ((z + x) > 5f) {
    float y = 5f;
    flatTernary0 = (y + x);
} else {
    flatTernary0 = 2f;
}
w = flatTernary0;");
        LinPrints(Ex.Lambda<Func<float, float, float>>(ex, x, zi), @"
(Func<float, float, float>) ((float x, float z) => {
    float flatTernary0;
    z = 5f;
    if ((z + x) > 5f) {
        float y = 5f;
        flatTernary0 = (y + x);
    } else {
        flatTernary0 = 2f;
    }
    return w = flatTernary0;
})");
        //ternary ok
        var ex2 = wi.Is(Ex.Condition(Ex.Block(zi.Is(ExC(5f)), ExC(true)),
            yi.Add(x),
            ExC(2f)
        ));
        LinPrints(ex2, @"
z = 5f;
w = (true ?
    (y + x) :
    2f);");
        //cond can be simplified
        var ex3 = Ex.Condition(Ex.GreaterThan(zi, ExC(5f)),
            yi.Add(x),
            ExC(2f)
        );
        LinPrints(ex3, @"(z > 5f) ?
    (y + x) :
    2f");
    }

    [Test]
    public void TestSwitch() {
        var x = Prm<int>("x");
        var typedSwitch = x.Is(Ex.Switch(typeof(int), x, Ex.Constant(3), null, Ex.SwitchCase(Ex.Constant(4), Ex.Constant(5))));
        var untypedSwitch = Ex.Switch(typeof(void), x, Ex.Constant(3), null, Ex.SwitchCase(Ex.Constant(4), Ex.Constant(5)));
        LinPrints(typedSwitch, @"
int flatSwitch0;
switch (x) {
    case (5):
        flatSwitch0 = 4;
        break;
    default:
        flatSwitch0 = 3;
        break;
}
x = flatSwitch0;
");
        LinPrints(untypedSwitch, @"
switch (x) {
    case (5):
        break;
    default:
        break;
}
");
    }

    [Test]
    public void TestNested() {
        var x = Prm<int>("x");
        var y = Prm<int>("y");
        var z = Prm<int>("z");
        var a = Prm<int>("a");
        LinPrints(Ex.Block(new[]{a}, 
            a.Is(ExC(1).Add(Ex.Block(new[]{z}, 
                z.Is(ExC(4).Add(Ex.Block(new[]{x, y}, 
                    x.Is(ExC(-1)),
                    y.Is(ExC(2)),
                    x.Is(x.Add(ExC(6))),
                    y.Is(x.Add(ExC(2)))
                    ))),
                z.Add(ExC(3))
                )))
            ), @"
int x = -1;
int y = 2;
x = (x + 6);
int z = (4 + (y = (x + 2)));
int a = (1 + (z + 3));
");
    }

    [Test]
    public void TestTry() {
        var x = Prm<int>("x");
        var y = Prm<int>("y");
        var z = Prm<int>("z");
        var exc1 = Prm<InvalidOperationException>("ioExc");
        var exc2 = Prm<InvalidCastException>("castExc");
        var tryBlock = Ex.MakeTry(typeof(int), Ex.Throw(Ex.New(typeof(Exception)), typeof(int)), Ex.Assign(x, ExC(5)), null, new[] {
            Ex.Catch(exc1,
                Ex.Block(new[] {z}, Ex.Assign(z, y), y),
                Ex.Equal(Ex.PropertyOrField(exc1, "Message"), ExC("hello"))),
            Ex.Catch(exc2, y)
        });
        LinPrints(Ex.Lambda<Func<int, int, int>>(x.Add(tryBlock), x, y), @"
(Func<int, int, int>) ((int x, int y) => {
    int flatTry0;
    int flatBlock1 = x;
    try {
        throw new Exception();
    } catch (InvalidOperationException ioExc) when (ioExc.Message == ""hello"") {
        int z = y;
        flatTry0 = y;
    } catch (InvalidCastException castExc) {
        flatTry0 = y;
    } finally {
        x = 5;
    }
    return flatBlock1 + flatTry0;
})");
        SafeLinPrints(Ex.Lambda<Func<int, int, int>>(x.Add(tryBlock), x, y), @"
(Func<int, int, int>) ((int x, int y) => {
    int flatTry0;
    int flatBlock1 = x;
    try {
        throw new Exception();
    } catch (InvalidOperationException ioExc) when (ioExc.Message == ""hello"") {
        int z = y;
        flatTry0 = y;
    } catch (InvalidCastException castExc) {
        flatTry0 = y;
    } finally {
        x = 5;
    }
    return flatBlock1 + flatTry0;
})");
    }

    [Test]
    public void TestSafeExec() {
        var x = Prm<int>("x");
        var ex = x.Is(Ex.Block(ExC(3), ExC(2).Add(ExC(5))).Add(Ex.Block(ExC(6), ExC(7).Sub(ExC(2)))));
        LinPrints(ex, @"
x = ((2 + 5) + (7 - 2));
");
        SafeLinPrints(ex, @"
int flatBlock0 = (2 + 5);
int flatBlock1 = (7 - 2);
x = (flatBlock0 + flatBlock1);
");
        var tex = x.Is(Ex.Block(ExC(3), ExC(2).Add(ExC(5))).Add(Ex.Block(ExC(6), 
            Ex.Throw(Ex.New(typeof(Exception)), typeof(int)))));
        LinPrints(tex, @"
int flatBlock0 = (2 + 5);
int flatBlock1 = throw new Exception();
x = (flatBlock0 + flatBlock1);
");
    }

    [Test]
    public void TestAndOr() {
        var x = Prm<bool>("x");
        var y = Prm<bool>("y");
        var z = Prm<bool>("z");
        var exAnd = z.Is(Ex.AndAlso(x, Ex.Block(ExC(5), y)));
        var exOr = z.Is(Ex.OrElse(Ex.Block(ExC(7), x), y));
        LinPrints(exAnd, @"
bool flatTernary0;
if (x) {
    flatTernary0 = y;
} else {
    flatTernary0 = false;
}
z = flatTernary0;
");
        LinPrints(exOr, @"
z = (x ?
    true :
    y);
");
    }


    [Test]
    public void TestInterference() {
        var x = Prm<float>("x");
        var y = Prm<float>("y");
        var ex1 = x.Add(Ex.Block(x.Is(ExC(5f)), x.Add(2)));
        //x is reassigned in the block
        LinPrints(ex1, @"
float flatBlock0 = x;
x = 5f;
float flatBlock1 = (x + 2f);
flatBlock0 + flatBlock1;
");
        var ex2 = x.Add(Ex.Block(Ex.PostIncrementAssign(x)));
        LinPrints(ex2, @"x + (x++)");
        
        var ex3 = x.Add(Ex.Block(new[]{y}, y.Is(ExC(5f)), Ex.PostIncrementAssign(x)));
        LinPrints(ex3, @"
float flatBlock0 = x;
float y = 5f;
float flatBlock1 = (x++);
flatBlock0 + flatBlock1;
");
        
        //methods are assumed to cause interference
        var ex4 = x.Add(Ex.Block(new[]{y}, y.Is(ExC(5f)), 
            Expression.Call(null, typeof(Math).GetMethod("Abs", new[]{typeof(float)})!, x)));
        LinPrints(ex4, @"
float flatBlock0 = x;
float y = 5f;
float flatBlock1 = Math.Abs(x);
flatBlock0 + flatBlock1;
");
        
        //local assignments are ok
        var ex5 = x.Add(Ex.Block(new[]{y}, y.Is(ExC(5f)), x));
        LinPrints(ex5, @"
float y = 5f;
x + x;
");
    }
    
}
}