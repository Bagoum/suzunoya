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
    
        private static float Compile(Ex ex) => Expression.Lambda<Func<float>>(ex).Compile()();
    private static T Compile<T>(Ex ex) => Expression.Lambda<Func<T>>(ex).Compile()();
    private static Func<float,float> Compile1(Ex ex, ParameterExpression pex) => Expression.Lambda<Func<float, float>>(ex, pex).Compile();
    private static ParameterExpression VF(string name) => Ex.Variable(typeof(float), name);
    private static ParameterExpression V<T>(string name) => Ex.Variable(typeof(T), name);

    private static void Prints(Expression ex, string expected) => 
        StringsApproxEqual(expected, ExPrinter.Print(ex));

    private static void LinPrints(Expression ex, string expected) =>
        StringsApproxEqual(expected.Replace("\r\n", "\n"), ExPrinter.LinearizePrint(ex));

    [Test]
    public void TestAssign() {
        var x = VF("x");
        var yi = VF("y");
        var zi = VF("z");

        var ex = x.Is(Ex.Block(new[] {yi},
            yi.Is(ExC(5f)),
            yi.Add(x)
        ));

        //Block on RHS is not printable
        Assert.Throws<Exception>(() => ExPrinter.Print(ex));
        LinPrints(ex, @"
float y = 5f;
x = (y + x);");

        AreEqual(11f, Compile1(ex.Linearize(), x)(6));

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
        var ex = Ex.Condition(Ex.Block(zi.Is(ExC(5f)), zi.Add(x).GT(ExC(5f))),
            Ex.Block(new[] {yi},
                yi.Is(ExC(5f)),
                yi.Add(x)),
            ExC(2f)
        );
        LinPrints(ex, @"
float flatTernary0;
z = 5f;
if ((z + x) > 5f) {
    float y = 5f;
    flatTernary0 = (y + x);
} else {
    flatTernary0 = 2f;
}
flatTernary0;");
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
    return flatTernary0;
})");
        //ternary ok
        var ex2 = Ex.Condition(Ex.Block(zi.Is(ExC(5f)), ExC(true)),
            yi.Add(x),
            ExC(2f)
        );
        LinPrints(ex2, @"
z = 5f;
true ?
    (y + x) :
    2f;");
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
    return x + flatTry0;
})");
    }
}
}