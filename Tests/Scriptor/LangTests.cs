using System;
using NUnit.Framework;
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
    
    private delegate int MyDelegateType(int a, int b, out EnvFrame ef);

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
}"
            .Compile<MyDelegateType>(D<int>("a"), D<int>("b"), CompileHelpers.OutEnvFrameArg);
        
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