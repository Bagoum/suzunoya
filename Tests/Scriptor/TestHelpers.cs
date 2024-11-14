using System;
using System.Linq;
using System.Text;
using Scriptor.Analysis;
using Scriptor.Compile;
using Scriptor.Reflection;

namespace Tests.Scriptor;

public static class TestHelpers {
    public static DelegateArg<T> D<T>(string name) => new(name);

    public static D Compile<D>(this string source, params IDelegateArg[] args) where D : Delegate =>
        CompileHelpers.ParseAndCompileDelegate<D>(source, args);

    public static T Value<T>(this string source, params IDelegateArg[] args) =>
        CompileHelpers.ParseAndCompileDelegate<ScriptFn<T>>(source, 
            args.Append(CompileHelpers.OutEnvFrameArg).ToArray()).Invoke(out _);

    public static string Debug(this EnvFrame ef) {
        var sb = new StringBuilder();
        DebugHelpers.DebugRootEf(sb, ef);
        return sb.ToString();
    }
}