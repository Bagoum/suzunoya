using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using NUnit.Framework;
using static BagoumLib.Unification.TypeDesignation;
using static BagoumLib.Unification.TypeTree;

namespace Tests.BagoumLib {
public static class TypeUnifyTests {
    public static Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> PossibleUnifiers(this ITree t) =>
        t.PossibleUnifiers(new TypeResolver(), Unifier.Empty);

    private static TypeDesignation Call(params TypeDesignation[] argsAndRet)
        => new Dummy(Dummy.METHOD_KEY, argsAndRet);

    private static Variable Unknown => new();
    private static ITree UnknownTree => new AtomicWithType(Unknown);
    private static Variable Types(params Known[] k) => new() { RestrictedTypes = k };
    private static TypeDesignation Type<T>() => TypeDesignation.FromType(typeof(T));
    private static Known K<T>() => TypeDesignation.FromType(typeof(T)) as Known ?? 
                                                 throw new Exception($"Type {typeof(T).RName()} is not known");
    private static Known Fixed(Type t, params TypeDesignation[] args) => 
        new(t, args);

    private static void AssertSuccess<T>(TypeDesignation left, TypeDesignation right) {
        Console.WriteLine(left.ToString());
        Console.WriteLine(right.ToString());
        var u = left.Unify(right, Unifier.Empty);
        if (u.IsRight)
            Assert.Fail($"Unification failed: {u.Right}");
        var lt = left.Resolve(u.Left);
        if (lt.IsRight)
            Assert.Fail($"Left resolution failed: {lt.Right}");
        var rt = right.Resolve(u.Left);
        if (rt.IsRight)
            Assert.Fail($"Right resolution failed: {rt.Right}");
        Assert.AreEqual(typeof(T), lt.Left);
        Assert.AreEqual(typeof(T), rt.Left);
        Console.WriteLine($"Unified to {typeof(T).RName()}");
        if ((left, right) is (Dummy ld, Dummy rd)) {
            var lParams = ld.Arguments.Skip(1).Select(a => a.Resolve(u.Left)).SequenceL();
            if (lParams.IsRight)
                Assert.Fail($"Left param resolution failed: {lParams.Right}");
            var rParams = rd.Arguments.Skip(1).Select(a => a.Resolve(u.Left)).SequenceL();
            if (rParams.IsRight)
                Assert.Fail($"Right param resolution failed: {rParams.Right}");
            Console.Write($"{ld.Typ.FirstToUpper()} param types: ");
            for (int ii = 0; ii < lParams.Left.Count; ++ii) {
                Assert.AreEqual(lParams.Left[ii], rParams.Left[ii]);
                if (ii > 0)
                    Console.Write(", ");
                Console.Write(lParams.Left[ii].RName());
            }
            Console.WriteLine("\n");
        }
    }
    
    private static void AssertFailure<Exc>(TypeDesignation left, TypeDesignation right) {
        Console.WriteLine("Beginning failure test");
        Console.WriteLine(left.ToString());
        Console.WriteLine(right.ToString());
        var u = left.Unify(right, Unifier.Empty);
        TypeUnifyErr err = null!;
        if (u.IsRight) {
            Console.WriteLine("Failed at unification step");
            err = u.Right;
            goto verify;
        }
        var lt = left.Resolve(u.Left);
        if (lt.IsRight) {
            Console.WriteLine("Failed at left resolution step");
            err = lt.Right;
            goto verify;
        }
        var rt = right.Resolve(u.Left);
        if (rt.IsRight) {
            Console.WriteLine("Failed at right resolution step");
            err = rt.Right;
            goto verify;
        }
        if ((left, right) is (Dummy ld, Dummy rd)) {
            var lParams = ld.Arguments.Skip(1).Select(a => a.Resolve(u.Left)).SequenceL();
            if (lParams.IsRight) {
                Console.WriteLine("Failed at left param step");
                err = lParams.Right;
                goto verify;
            }
            var rParams = rd.Arguments.Skip(1).Select(a => a.Resolve(u.Left)).SequenceL();
            if (rParams.IsRight) {
                Console.WriteLine("Failed at right param step");
                err = rParams.Right;
                goto verify;
            }
        }
        Assert.Fail("Did not fail!");
        
        verify: ;
        Assert.IsInstanceOf<Exc>(err);

    }

    private static MethodInfo M(string name) => typeof(TypeUnifyTests).GetMethod(name)!;
    private static MethodInfo MT(string name, params Type[] types) => typeof(TypeUnifyTests).GetMethod(name, types)!;
    
    private static Dummy FromMethodN(string name) => FromMethod(M(name));

    public static List<T> Generic0<T>(T arg1) => null!;
    public static List<T> Generic1<T>(List<T[]> arg1, Func<T, T> arg2) => null!;
    public static T[] Generic2<T>(T[] arg1, Func<T, T> arg2) => default!;
    
    
    [Test]
    public static void TestUnification() {
        AssertSuccess<List<int>>(FromMethodN("Generic0"), Call(Type<int>(), Unknown));
        
        AssertFailure<TypeUnifyErr.NotEqual<Known>>(FromMethodN("Generic0"), Call(Unknown, Type<int>()));
        
        AssertSuccess<List<int>>(FromMethodN("Generic0"), Call(Unknown, Type<List<int>>()));
        
        AssertSuccess<List<float>>(FromMethodN("Generic1"), Call(
            Unknown, 
            Fixed(typeof(Func<,>), Type<float>(), Unknown),
            Unknown));
        
        AssertFailure<TypeUnifyErr.NotEqual<Known>>(FromMethodN("Generic1"), Call(
            Unknown, 
            Fixed(typeof(Func<,>), Type<float>(), Type<int>()),
            Unknown));
        
        AssertSuccess<float[]>(FromMethodN("Generic2"), Call( 
            Unknown, 
            Fixed(typeof(Func<,>), Type<float>(), Unknown),
            Unknown));
        
    }

    
    public static T[] NestA<T>(int a, T b) => default!;
    public static T[] NestB<T>(T b) => default!;
    public static T ID<T>(T b) => default!;
    private static readonly Dummy id = FromMethod(M("ID"));
    [Test]
    public static void TestNesting() {
        //let's say we call NestA(5, NestB("hello"))
        var mb = FromMethodN("NestB");
        var ma = FromMethodN("NestA");
        //We have some kind of AST with this kind of structure
        ITree cb = new Method(new[]{mb}, new AtomicWithType(Type<string>()));
        ITree ca = new Method(new[]{ma}, new AtomicWithType(Type<int>()), cb);
        var opts = ca.PossibleUnifiers().LeftOrThrow;
        Assert.AreEqual(opts.Count, 1);
        var u = ca.ResolveUnifiers(opts[0].Item1, new(), opts[0].Item2).LeftOrThrow;
        Assert.AreEqual(typeof(string[][]), ca.SelectedOverloadReturnType.Resolve(Unifier.Empty).LeftOrThrow);
        Assert.AreEqual(typeof(string[]), cb.SelectedOverloadReturnType.Resolve(Unifier.Empty).LeftOrThrow);
        
        cb = new Method(new[]{mb}, UnknownTree);
        ca = new Method(new[]{ma}, UnknownTree, cb);
        opts = ca.PossibleUnifiers().LeftOrThrow;
        Assert.AreEqual(opts.Count, 1);
        //One overload found with type T[][] (further specification not possible)
        Assert.IsTrue(opts[0].Item1 is Known kt 
                      && kt.Arguments[0] is Known kt2 
                      && kt2.Arguments[0] is Variable);
        var ue = ca.ResolveUnifiers(Type<string[][][]>(), new(), Unifier.Empty);
        Assert.AreEqual(typeof(string[][]), cb.SelectedOverloadReturnType.Resolve(Unifier.Empty).LeftOrThrow);

        ue = ca.ResolveUnifiers(Type<string>(), new(), Unifier.Empty); //string doesnt match T[]
        Assert.IsInstanceOf<TypeUnifyErr.NotEqual<Known>>(ue.Right);


    }

    public static string Add(int a, string b) => default!;
    private static readonly Dummy add1 = FromMethod(MT("Add", typeof(int), typeof(string)));
    public static int Add(int a, float b) => default!;
    private static readonly Dummy add2 = FromMethod(MT("Add", typeof(int), typeof(float)));
    public static T AddG<T>(T a, T b) => default!;
    private static readonly Dummy addG = FromMethod(M("AddG"));
    public static T AddG2<T>(float a, T b) => default!;
    private static readonly Dummy addG2 = FromMethod(M("AddG2"));
    [Test]
    public static void TestOverloading() {
        ITree ast = new Method(new[] {
            add1, add2
        }, new AtomicWithType(Type<int>()), 
            new AtomicWithType(Type<int>()));
        Assert.IsInstanceOf<TypeUnifyErr.NoPossibleOverload>(ast.PossibleUnifiers().Right);

        ast = new Method(new[] {
            add1, add2
        }, new AtomicWithType(Unknown), 
            new AtomicWithType(Type<float>()));
        //Only the second function takes a second argument of float
        AssertPossibleTypes(ast, typeof(int));
        
        ast = new Method(new[] {
                add1, add2
            }, new AtomicWithType(Type<int>()), 
            new AtomicWithType(Unknown));
        //Both functions take a second argument of int, so this is ambiguous
        AssertPossibleTypes(ast, typeof(string), typeof(int));
        Assert.IsInstanceOf<TypeUnifyErr.NoResolvableOverload>(ast.ResolveUnifiers(Type<float>(), new(), Unifier.Empty).Right);
        var u = ast.ResolveUnifiers(Type<int>(), new(), Unifier.Empty).LeftOrThrow;
        Assert.AreEqual(typeof(int), ast.SelectedOverloadReturnType.Resolve(Unifier.Empty).Left);

        ast = new Method(new[] {
                add1, add2, addG
            }, new AtomicWithType(Type<float>()),
            new AtomicWithType(Unknown));
        //Only the generic takes a first argument of type float, and it is resolved to float
        AssertPossibleTypes(ast, typeof(float));
        
        ast = new Method(new[] {
                add1, add2, addG2
            }, new AtomicWithType(Type<float>()),
            new AtomicWithType(Unknown));
        //The second generic takes a first argument of float, but that underspecifies it
        AssertPossibleTypes(ast, null as Type);
        
        ast = new Method(new[] {
                 addG2
            }, new AtomicWithType(Type<float>()),
            new AtomicWithType(Type<double>()));
        //This specifies the generic of addG2
        AssertPossibleTypes(ast, typeof(double));
        
        //Nested tests with overloading support
        ast = new Method(new[] { addG }, 
            new AtomicWithType(Type<float>(), Type<double>()),
            new Method(new[] { addG2 } ,
                new AtomicWithType(Unknown),
                new AtomicWithType(Type<string>(), Type<double>())
            ));
        AssertPossibleTypes(ast, typeof(double));
        
        ast = new Method(new[] { addG }, 
            new AtomicWithType(Type<float>()),
            new Method(new[] { addG2 } ,
                new AtomicWithType(Unknown),
                new AtomicWithType(Type<string>(), Type<double>())
            ));
        Assert.IsInstanceOf<TypeUnifyErr.NoPossibleOverload>(ast.PossibleUnifiers().Right);
        
        ast = new Method(new[] { addG }, 
            new AtomicWithType(Type<float>(), Type<double>()),
            new Method(new[] { addG2 } ,
                new AtomicWithType(Unknown),
                new AtomicWithType(Unknown)
            ));
        AssertPossibleTypes(ast, typeof(float), typeof(double));
        
        ast = new Method(new[] { addG2 },
            //the first arg to G2 must be a float, so the contents of addG can be unified,
            //which you can see in the unifier output
            new Method(new[] { addG } ,
                new AtomicWithType(Unknown),
                new AtomicWithType(Unknown)
            ),
            new AtomicWithType(Unknown));
        AssertPossibleTypes(ast, null as Type);
        
    }

    public static char AddList1(float[] a, double[] b) => default!;
    public static T[] ConcatList<T>(float[] a, T[] b) => default!;
    [Test]
    public static void TestRestrictedVarTypes() {
        var sdv = Types(K<string>(), K<double>());
        //simple method usage
        IMethodTree ast = new Method(new[] { add1 },
            new AtomicWithType(Types(K<float>(), K<int>())),
            new AtomicWithType(sdv)
        );
        AssertPossibleTypes(ast, typeof(string));
        var u = ast.ResolveUnifiers(K<string>(), new(), Unifier.Empty).LeftOrThrow;
        Assert.AreEqual(K<int>(), ast.Arguments[0].SelectedOverloadReturnType);
        Assert.AreEqual(K<string>(), ast.Arguments[1].SelectedOverloadReturnType);
        
        //now let's add list generic casts
        var tEle = new Variable();
        var res = new TypeResolver(new ImplicitTypeConverter(Dummy.Method(tEle.MakeArrayType(), tEle)));
        ast = new Method(new[] { FromMethodN("AddList1") },
            new AtomicWithType(Types(K<float>(), K<int>())),
            new AtomicWithType(sdv)
        );
        AssertPossibleTypes(ast, res, typeof(char));
        u = ast.ResolveUnifiers(K<char>(), res, Unifier.Empty).LeftOrThrow;
        Assert.AreEqual(K<float[]>(), ast.Arguments[0].SelectedOverloadReturnType);
        Assert.AreEqual(K<double[]>(), ast.Arguments[1].SelectedOverloadReturnType);

        ast = new Method(new[] { FromMethodN("ConcatList") },
            new AtomicWithType(Types(K<float>(), K<int>())),
            new AtomicWithType(sdv)
        );
        //our return type is now (string|double)[]
        AssertHelpers.ListEq(new[]{sdv.MakeArrayType()}, 
            ast.PossibleUnifiers(res, Unifier.Empty).LeftOrThrow.Select(x => x.Item1).ToArray());
        u = ast.ResolveUnifiers(sdv.MakeArrayType(), res, Unifier.Empty).LeftOrThrow;
        Assert.AreEqual(K<float[]>(), ast.Arguments[0].SelectedOverloadReturnType);
        Assert.AreEqual(sdv.MakeArrayType(), ast.Arguments[1].SelectedOverloadReturnType);
        Assert.IsFalse(ast.IsFullyResolved);
        
        //we can avoid the ambiguity if any of the restricted types don't require implicit casts,
        // since implicit casts are conservative
        var sdav = Types(K<string>(), K<double[]>());
        ast = new Method(new[] { FromMethodN("ConcatList") },
            new AtomicWithType(Types(K<float>(), K<int>())),
            new AtomicWithType(sdav)
        );
        AssertPossibleTypes(ast, res, typeof(double[]));
        u = ast.ResolveUnifiers(K<double[]>(), res, Unifier.Empty).LeftOrThrow;
        Assert.AreEqual(K<float[]>(), ast.Arguments[0].SelectedOverloadReturnType);
        Assert.AreEqual(K<double[]>(), ast.Arguments[1].SelectedOverloadReturnType);
        Assert.IsTrue(ast.IsFullyResolved);

        //intersection
        ast = new Method(new[] { addG }, //t->t->t
            new AtomicWithType(Types(K<float>(), K<double>())),
            new Method(new[] { addG2 } , //float->t->t
                new AtomicWithType(Unknown),
                new AtomicWithType(Types(K<string>(), K<double>()))
            ));
        AssertPossibleTypes(ast, typeof(double));
        u = ast.ResolveUnifiers(K<double>(), new(), Unifier.Empty).LeftOrThrow;
    }
    
    

    [Test]
    public static void TestImplicitCast() {
        var addG2Ast = new Method(new[] { addG2 },
            new AtomicWithType(Unknown),
            //Second argument to add2 must be float, so by default this (float->X->X) won't typecheck
            new AtomicWithType(Type<int>(), Type<string>())
        );
        ITree ast = new Method(new[] { add2 }, 
            new AtomicWithType(Unknown),
            addG2Ast);
        Assert.IsInstanceOf<TypeUnifyErr.NoPossibleOverload>(ast.PossibleUnifiers().RightOrThrow);
        var resolver = new TypeResolver(new Dictionary<Type, Type[]> {
            { typeof(int), new[] { typeof(float) } }
        });
        AssertPossibleTypes(ast, resolver, typeof(int)); //int is the return type of add2
        var u = ast.ResolveUnifiers(Type<int>(), resolver, Unifier.Empty).LeftOrThrow;
        //Implicit casts cascade as far down as possible
        Assert.IsNull(ast.ImplicitCast);
        Assert.IsNull(addG2Ast.ImplicitCast);
        Assert.AreEqual(typeof(float), addG2Ast.Arguments[1].ImplicitCast.ResultType.Resolve(Unifier.Empty).LeftOrThrow);

        var isResolver = new TypeResolver(new Dictionary<Type, Type[]> {
            { typeof(int), new[] { typeof(float), typeof(string) } },
            { typeof(string), new[] { typeof(float) } }
        });
        //We get two ints because two overloads work with implicit casts
        //we can dedupe but it'll end up throwing later anyways
        AssertPossibleTypes(ast, isResolver, typeof(int), typeof(int));
        Assert.IsInstanceOf<TypeUnifyErr.MultipleImplicits>(ast.ResolveUnifiers(Type<int>(), isResolver, Unifier.Empty).RightOrThrow);
        
        var dResolver = new TypeResolver(new Dictionary<Type, Type[]> {
            { typeof(int), new[] { typeof(string) } },
            { typeof(double), new[] { typeof(float) } }
        });
        Assert.IsInstanceOf<TypeUnifyErr.NoPossibleOverload>(ast.PossibleUnifiers(dResolver, Unifier.Empty).RightOrThrow);


        ITree nB = new Method(new[] { FromMethodN("NestB") }, // t->t[]
            new AtomicWithType(Type<int>(), Type<string>())
        );
        ast = new Method(new[] { addG },  // t->t->t
            new AtomicWithType(Type<float[]>()),
            nB);
        Assert.IsInstanceOf<TypeUnifyErr.NoPossibleOverload>(ast.PossibleUnifiers().RightOrThrow);
        
        Assert.IsInstanceOf<TypeUnifyErr.NoPossibleOverload>(ast.PossibleUnifiers(resolver, Unifier.Empty).RightOrThrow);
        //The above call doesn't work even with a resolver
        //This is because it requires "callee-internal" casting, as opposed to "callee-external" casting
        //Consider this tree:
        // Add::T->T->T
        //   5.0f::float
        //   Len::string->int
        //     "hello"::string
        //In this tree, Add can convert the return type <int> of Len to a float. This is "callee-external"
        //But in this tree:
        // Add::T->T->T
        //   {5.0f}::float[]
        //   Nest::T->T[]
        //     5::int
        //In this tree, Add can't convert the return type <int[]> of Nest to <float[]>. Theoretically,
        // the implicit cast could work if Add was able to request that Nest produce a value of type float[].
        // However, this is a different problem scope, and **it doesn't even work in C#**.
        //This doesn't compile!
        //  AddG<float[]>(new float[0], NestB(5));

        ast = new Method(new[] { addG },  // t->t->t
            new AtomicWithType(Type<string>()), //this gets cast to float
            new Method(new[] { addG },  // t->t->t
                new AtomicWithType(Type<float>()),
                new AtomicWithType(Type<int>()) //this gets cast to float
            ));
        Assert.IsInstanceOf<TypeUnifyErr.NoPossibleOverload>(ast.PossibleUnifiers().RightOrThrow);
        AssertPossibleTypes(ast, isResolver, typeof(float));
        u = ast.ResolveUnifiers(Type<float>(), isResolver, Unifier.Empty).LeftOrThrow;
        Assert.IsNotNull((ast as Method).Arguments[0].ImplicitCast);
        Assert.IsNotNull(((ast as Method).Arguments[1] as Method).Arguments[1].ImplicitCast);
        int w = 5;
    }

    public static string FloatArr(float[] fs) => default!;
    public static int StringArr(string[] ss) => default!;
    private static readonly Dummy floatarr = FromMethod(M("FloatArr"));
    private static readonly Dummy stringarr = FromMethod(M("StringArr"));

    [Test]
    public static void TestGenericImplicitCast() {
        ITree ast = new Method(new[] { floatarr }, new AtomicWithType(Type<float>()));
        var tArrEle = new Variable();
        var isResolver = new TypeResolver(new ImplicitTypeConverter(Dummy.Method(tArrEle.MakeArrayType(), tArrEle)));
        Assert.IsInstanceOf<TypeUnifyErr.NoPossibleOverload>(ast.PossibleUnifiers().RightOrThrow);
        AssertPossibleTypes(ast, isResolver, typeof(string));
        var u = ast.ResolveUnifiers(Type<string>(), isResolver, Unifier.Empty).LeftOrThrow;

        //ensure that the same implicit generic converter can be used multiple times with different types
        ast = new Method(new[] { stringarr }, ast);
        AssertPossibleTypes(ast, isResolver, typeof(int));
        u = ast.ResolveUnifiers(Type<int>(), isResolver, Unifier.Empty).LeftOrThrow;
        int k = 5;
    }

    
    public static float Consume<T>(float a, T b) => default!;
    public static float Consume2<T>(T a, T b) => default!;
    public static float ConsumeInt(int a) => default!;
    public static float ConsumeString(string a) => default!;
    private static readonly Dummy consume = FromMethod(M("Consume"));
    private static readonly Dummy consume2 = FromMethod(M("Consume2"));
    private static readonly Dummy consumeInt = FromMethod(M("ConsumeInt"));
    private static readonly Dummy consumeStr = FromMethod(M("ConsumeString"));
    [Test]
    public static void TestConsumption() {
        //Not enough information to fully typecheck, even though return type can be determined
        ITree ast = new Method(new[] { consume }, // float->T->float
            UnknownTree,
            UnknownTree
        );
        AssertPossibleTypes(ast, typeof(float));
        _ = ast.ResolveUnifiers(Type<float>(), new(), Unifier.Empty).LeftOrThrow;
        Assert.IsFalse(ast.IsFullyResolved);
        
        //When going top-down, `consume` cannot provided a realized `T` for `addG`
        ast = new Method(new[] { consume }, // float->T->float
            UnknownTree,
            new Method(new[] { addG }, //T->T->T
                UnknownTree, //This node gets initially finalized with a free variable
                new AtomicWithType(Type<int>())
            )
        );
        AssertPossibleTypes(ast, typeof(float));
        var ue = ast.ResolveUnifiers(Type<float>(), new(), Unifier.Empty);
        var free = ((ast as Method).Arguments[1] as Method).Arguments[0];
        //Initially unbound
        Assert.IsInstanceOf<Variable>(free.SelectedOverloadReturnType);
        //Bound in a third readonly pass
        ast.FinalizeUnifiers(ue.Left.Item2);
        Assert.IsInstanceOf<Known>(free.SelectedOverloadReturnType);
        Assert.AreEqual(typeof(int), free.SelectedOverloadReturnType.Resolve(Unifier.Empty).LeftOrThrow);
        Assert.IsTrue(ast.IsFullyResolved);

        //This requires FinalizeOverload to prune out overloads that don't match arguments
        ast = new Method(new[] { consumeInt, consumeStr },
            new AtomicWithType(Type<int>())
        );
        AssertPossibleTypes(ast, typeof(float));
        ue = ast.ResolveUnifiers(Type<float>(), new(), Unifier.Empty);
        Assert.AreEqual(typeof(float), ue.LeftOrThrow.Item1.Resolve(Unifier.Empty).LeftOrThrow);
        Assert.IsTrue(ast.IsFullyResolved);

        var v1 = UnknownTree;
        ast = new Method(new[] { consumeInt, consumeStr },
            v1
        );
        AssertPossibleTypes(ast, typeof(float), typeof(float));
        Assert.IsInstanceOf<TypeUnifyErr.MultipleOverloads>(ast.ResolveUnifiers(Type<float>(), new(), Unifier.Empty).RightOrThrow);
        (ast as Method).PreferFirstOverload = true;
        var u = ast.ResolveUnifiers(Type<float>(), new(), Unifier.Empty).LeftOrThrow.Item2;
        Assert.AreEqual(typeof(int), v1.SelectedOverloadReturnType.Resolve(u).LeftOrThrow);
        
        
        /* consume t>t>float
            id t>t
                unknown
            id t>t
                string
        */
        ast = new Method(new[] { consume2 },
            new Method(new[] { id }, UnknownTree),
            new Method(new[] { id }, new AtomicWithType(Type<string>()))
        );
        var opts = ast.PossibleUnifiers().LeftOrThrow;
        _ = ast.ResolveUnifiers(opts[0].Item1, new(), opts[0].Item2).LeftOrThrow;
        Assert.IsTrue(ast.IsFullyResolved);

        ast = new Method(new[] { consume2 },
            new Method(new[] { consumeInt }, UnknownTree),
            new Method(new[] { consume }, UnknownTree, UnknownTree) //second unknown can't be resolved
        );
        opts = ast.PossibleUnifiers().LeftOrThrow;
        _ = ast.ResolveUnifiers(opts[0].Item1, new(), opts[0].Item2).LeftOrThrow;
        Assert.IsFalse(ast.IsFullyResolved);
        
        var myVar = new AtomicWithType(new Variable());
        ast = new Method(new[] { consume2 },
            new Method(new[] { consumeInt }, myVar), //myVar is int here
            new Method(new[] { consume }, UnknownTree, myVar) //so myVar becomes int here, since the unifier is threaded
        );
        opts = ast.PossibleUnifiers().LeftOrThrow;
        ue = ast.ResolveUnifiers(opts[0].Item1, new(), opts[0].Item2).LeftOrThrow;
        Assert.IsTrue(ast.IsFullyResolved);
        
        
        int w = 5;
    }

    
    public static T First<T>(List<T> a) => default!;
    public static Func<int, T[]> ExFunc<T>(T a) => default!;
    [Test]
    public static void RewriteImplicitCast() {
        ITree ast = new Method(new[] { FromMethodN("First") },
            new Method(new[] { FromMethodN("ExFunc") },
                new AtomicWithType(Type<float>()))
        );
        //obviously, doesn't compile by default
        Assert.IsInstanceOf<TypeUnifyErr.NoPossibleOverload>(ast.PossibleUnifiers().RightOrThrow);
        //let's add a generic cast...
        var v1 = Unknown;
        var resolver = new TypeResolver(new ImplicitTypeConverter(Dummy.Method(
            new Known(typeof(List<>), v1),
            new Known(typeof(Func<,>), Type<int>(), new Known(Known.ArrayGenericType, v1))
        )));
        //now it works
        var ue = ast.PossibleUnifiers(resolver, Unifier.Empty).LeftOrThrow;
        var u = ast.ResolveUnifiers(ue[0].Item1, resolver, ue[0].Item2).LeftOrThrow;
        Assert.AreEqual(u.Item1.Resolve(u.Item2).LeftOrThrow, typeof(float));
        Assert.IsNotNull((ast as Method).Arguments[0].ImplicitCast);
        var w = 5;

    }
    
    
    
    private static void AssertPossibleTypes(ITree tree, params Type?[] typs) =>
        AssertPossibleTypes(tree, new(), typs);
    private static void AssertPossibleTypes(ITree tree, TypeResolver res, params Type?[] typs) {
        var tds = tree.PossibleUnifiers(res, Unifier.Empty);
        if (tds.IsRight)
            Assert.Fail(tds.Right.ToString());
        AssertHelpers.ListEq(
            tds.Left.Select(t => t.Item1.Resolve(Unifier.Empty).LeftOrNull).ToList(),
            typs);
    }

}
}