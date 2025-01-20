using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scriptor;
using Scriptor.Analysis;
using Scriptor.Compile;
using static NUnit.Framework.Assert;
using static Tests.AssertHelpers;
using static Tests.TScriptor.TestHelpers;

namespace Tests.TScriptor {
public class LangTests {
    
    [OneTimeSetUp]
    public static void Setup() {
        _ = new DefaultLangCustomizer();
    }

    [Test]
    public static void TestOperators() {
        Assert.AreEqual(64, @"2 ^ 2 ^ 3".Value<float>());
        Assert.AreEqual(60, @"2 ^ 2 ^- 3".Value<float>());
        Assert.AreEqual(-64, @"-2 ^^ 2 ^ 3".Value<float>());
        Assert.AreEqual(2, "2 * 6 / 2 / 2 - 1".Value<float>());
        Assert.AreEqual(2, "2 + 6 * 2 / 4 - 3".Value<float>());
        var result = @"
var x = 5.0;
var y = x++ + 6 / 2;
x-- * 100 + ++y
".ValueEF<float>(out var ef);
        Assert.AreEqual(609, result);
        
        StringsApproxEqual(@"Variables:
	x<float>: 5
	y<float>: 9
Functions:", ef.Debug());
    }

    [Test]
    public static void TestOperatorsErrMsg() {
        Assert.AreEqual(-1, "2 + (2 - 5)".Value<float>());
        AssertHelpers.ThrowsMessage("Expected term", () => "2 + (2 - 5 + )".Value<float>());
        AssertHelpers.ThrowsMessage("partial function application", () => "var w = $()".Value<float>());
        //well-formed (but f,g don't exist)
        "var w = $(f, g 6)".AssertFailsAnnotation("any method by the name `f`");
        //5 6 cannot be a partial function so observing 6 is a fatal
        AssertHelpers.ThrowsMessage("partial function application.*CloseParen", () => "var w = $(f, 5 6)".Value<float>());
        AssertHelpers.ThrowsMessage("partial function application", () => "var w = $ (f, x, y)".Value<float>());
        Assert.AreEqual(-5, "2.0\n-5.0".Value<float>());
        AssertHelpers.ThrowsMessage("not expect implicit break before infix", () => "2.0\n- 5.0".Value<float>());
        Assert.AreEqual(-3, "2.0\n\t- 5.0".Value<float>());
        AssertHelpers.ThrowsMessage("not expect whitespace after prefix", () => "true & ! false".Value<bool>());
        AssertHelpers.ThrowsMessage("not expect whitespace after prefix", () => "true & !! false".Value<bool>());
        //this one doesn't work since nonfatals that bubble up to `statement` choice are silenced
        //AssertHelpers.ThrowsMessage("not expect whitespace after prefix", () => "! false".Value<bool>());
        AssertHelpers.ThrowsMessage("not expect whitespace before postfix", () => "5 ++ + 4".Value<bool>());
        AssertHelpers.ThrowsMessage("function application.*Operator: :", () => "BMath.Mod<T>(false ? 2)".Value<float>());
        AssertHelpers.ThrowsMessage("function application.*Operator: :", () => "BMath.Mod(false ? 2)".Value<float>());
    }

    [Test]
    public static void TestPartialFn() {
        //well-formed curry across lines
        "f\n\tx".AssertFailsAnnotation("no static method");
        "f\n\tx\n\ty".AssertFailsAnnotation("no static method");
        //x -> y dedent is not an implicit break since the block indent is 0
        "f\n\t\tx\n\ty".AssertFailsAnnotation("no static method");
        //reports curry failure or allow closing paren
        AssertHelpers.ThrowsMessage("increase the indentation.*CloseParen", () => "(f\nx)".Value<float>());
        AssertHelpers.ThrowsMessage("increase the indentation.*CloseParen", () => "(2, f\nx)".Value<float>());
        //if there's no parentheses, then f will be treated as a value and x as a value
        "f\nx".AssertFailsAnnotation("what \"f\" refers to");
        //reports curry failure or require end of line
        AssertHelpers.ThrowsMessage("whitespace between curried.*semicolon", () => "f (x)y".Value<float>());
    }
    
    private delegate int MyDelegateType(int a, int b, out EnvFrame ef);

    [Test]
    public static void TestMembers() {
        var result = @"
        var lis = new List<int>();
        lis.Add(5);
        var ct = lis.Count;
        var s = Math.Round(BMath.PI as double);
        BMath.Mod(4., 7.)
".ValueEF<int>(out var ef);
        Assert.AreEqual(3, result);
        
        StringsApproxEqual(@"Variables:
	lis<List<int>>: { 5 }
	ct<int>: 1
	s<double>: 3
Functions:", ef.Debug());
    }

    [Test]
    public static void TestMemberErr() {
        _ = "Math.Round<int>()".Parse();
        AssertHelpers.ThrowsMessage("Line 1, Cols 5-16.*generic function", () => "Math.Round<int>".Parse());
        "Math.".AssertFailsAnnotation("1-6.*after this period");//special cased to parse but fail in annotation
    }

    [Test]
    public static void TestFn() {
        var fn = @"
var c = a * 2;
function increment():: void {
    b++;
    c++;
}
b + block{
    increment();
    c;
}".Compile<MyDelegateType>(D<int>("a"), D<int>("b"), CompileHelpers.OutEnvFrameArg);
        
        AreEqual(1245, fn(120, 1004, out var ef));
        
        StringsApproxEqual(@"Variables:
	a<int>: 120
	b<int>: 1005
	c<int>: 241
Functions:
	void increment()", ef.Debug());
    }
    
    [Test]
    public static void Test1() {
        var script = @"
var x = 5;
x + y
";
        ThrowsMessage("Could not determine what \"y\" refers to", 
            () => script.Compile<Func<float, float>>(D<float>("z")));
        var fn = script.Compile<Func<float, float>>(D<float>("y"));
        AreEqual(15, fn(10));
    }

    [Test]
    public static void TestFnLexicalScope() {
        AreEqual(285, @"
var total = 0.0;
for (var ii = 0.; ii < 10; ++ii) {
    function getSquare() {
        return ii * ii;
    }
    total += getSquare();
}
total".Value<float>());
    }

    [Test]
    public static void TestArrayType() {
        AreEqual(new[]{1,2,3}, @"{1., 2., 3.}".Value<int[]>());
        AreEqual(new[]{1,2,3}, @"{1, 2, 3}".Value<int[]>());
        AreEqual(new[]{1,2,3}, @"{1, 2, 3}".Value<float[]>());
        //nested implicit casts don't work
        ThrowsMessage(@"return type was one of: int\[\]", () => @"{1., 2., 3.}".Value<float[]>());
        //but the array can be provided a type
        AreEqual(new[]{1f,2f,3f}, @"{1., 2., 3.}::float[]".Value<float[]>());
    }
}
}