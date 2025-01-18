using System;
using System.Linq;
using System.Text;
using BagoumLib.Functional;
using NUnit.Framework;
using Scriptor;
using Scriptor.Analysis;
using Scriptor.Compile;
using Scriptor.Reflection;

namespace Tests.TScriptor;

public static class TestHelpers {
    public static DelegateArg<T> D<T>(string name) => new(name);

    public static ST.Block Parse(this string source) =>
        CompileHelpers.Parse(ref source, out _);

    public static (IAST, LexicalScope) ParseAnn(this string source) =>
        CompileHelpers.ParseAnnotate(ref source);

    public static void AssertFailsAnnotation(this string source, string pattern) {
        var (ast, scope) = source.ParseAnn();
        var errs = ast.FirstPassExceptions().ToArray();
        if (errs.Length == 0)
            Assert.Fail("Expected annotation to fail, but it succeeded");
        foreach (var e in errs) {
            if (AssertHelpers.CheckRegexMatches(pattern, e.Message)) {
                AssertHelpers.RegexMatches(pattern, e.Message); //print the message
                return;
            }
        }
        Assert.Fail($"Could not find pattern\n{pattern}\nin any of the following exceptions:\n{string.Join("\n", errs.Select(x=>x.Message))}");
    }

    public static D Compile<D>(this string source, params IDelegateArg[] args) where D : Delegate =>
        CompileHelpers.ParseAndCompileDelegate<D>(source, args);

    public static T Value<T>(this string source, params IDelegateArg[] args) =>
        CompileHelpers.ParseAndCompileDelegate<ScriptFn<T>>(source, 
            args.Append(CompileHelpers.OutEnvFrameArg).ToArray()).Invoke(out _);
    
    public static T ValueEF<T>(this string source, out EnvFrame ef, params IDelegateArg[] args) =>
        CompileHelpers.ParseAndCompileDelegate<ScriptFn<T>>(source, 
            args.Append(CompileHelpers.OutEnvFrameArg).ToArray()).Invoke(out ef);

    public static string Debug(this EnvFrame ef) {
        var sb = new StringBuilder();
        DebugHelpers.DebugRootEf(sb, ef);
        return sb.ToString();
    }
}