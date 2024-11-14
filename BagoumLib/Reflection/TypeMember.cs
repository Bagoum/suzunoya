using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BagoumLib.Reflection;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;

namespace BagoumLib.Reflection;
/// <summary>
/// Abstraction over class members (methods, constructors, fields, properties).
/// </summary>
[PublicAPI]
public abstract class TypeMember {
    /// <summary>
    /// The parameters to this member. If this is an instance member (eg. string.Length), then the first
    ///  element is the instance parameter.
    /// </summary>
    public abstract NamedParam[] Params { get; }
    
    /// <summary>
    /// Return type of this member.
    /// </summary>
    public abstract Type ReturnType { get; }
    
    /// <summary>
    /// Whether or not this member is static.
    /// </summary>
    public abstract bool Static { get; }
    
    /// <summary>
    /// The base <see cref="MemberInfo"/> that this abstraction describes.
    /// </summary>
    public abstract MemberInfo BaseMi { get; }
    
    /// <summary>
    /// The simplified name of the type containing this member.
    /// </summary>
    public string TypeName => BaseMi.ReflectedType!.SimpRName();
    
    /// <summary>
    /// <see cref="BaseMi"/>.GetCustomAttribute
    /// </summary>
    public T? GetAttribute<T>() where T : Attribute => BaseMi.GetCustomAttribute<T>();
    
    /// <summary>
    /// Invoke this member. If this is an instance member, then `instance` must be provided as an instance
    ///  on which to invoke the member; if this is a static member, then `instance` must be null.
    /// </summary>
    public abstract object? InvokeInst(object? instance, params object?[] args);
    
#pragma warning disable CS8634
    /// <summary>
    /// Invoke this member. If this is an instance member, then the first element in `args` must be the instance.
    /// </summary>
    public object? Invoke(params object?[] args) => InvokeInst(MaybeSplitInstance(ref args), args);
#pragma warning restore CS8634
    
    /// <summary>
    /// Creates an expression representing the invocation of this member.
    /// If this is an instance member, then `instance` must be provided as an instance
    ///  on which to invoke the member; if this is a static member, then `instance` must be null.
    /// </summary>
    public abstract Ex InvokeExInst(Ex? instance, params Ex[] args);
    
    /// <summary>
    /// Creates an expression representing the invocation of this member.
    /// If this is an instance member, then the first element in `args` must be the instance.
    /// </summary>
    public Ex InvokeEx(params Ex[] args) => InvokeExInst(MaybeSplitInstance(ref args), args);

    /// <summary>
    /// The signature of this method without any parameter names.
    /// </summary>
    /// <returns></returns>
    public abstract string TypeOnlySignature();
    
    /// <summary>
    /// The signature of this method, where the representation of each parameter is defined by `paramMod`.
    /// </summary>
    public abstract string AsSignature(Func<NamedParam, int, string> paramMod);

    /// <summary>
    /// Create a <see cref="TypeMember"/> representing a <see cref="MemberInfo"/>, or throw an exception.
    /// </summary>
    public static TypeMember Make(MemberInfo mi) =>
        MaybeMake(mi) ?? throw new ArgumentOutOfRangeException(mi.GetType().ToString());
    
    /// <summary>
    /// Create a <see cref="TypeMember"/> representing a <see cref="MemberInfo"/>
    ///  that is a field or property, or throw an exception.
    /// </summary>
    public static Writeable MakeWriteable(MemberInfo mi) {
        var res = MaybeMake(mi);
        return res as Writeable ?? throw new Exception($"Member {mi.Name} is not writeable");
    }

    /// <summary>
    /// Attempt to create a <see cref="TypeMember"/> representing a <see cref="MemberInfo"/>.
    /// </summary>
    public static TypeMember? MaybeMake(MemberInfo mi) => mi switch {
        MethodInfo meth => new Method(meth),
        ConstructorInfo cons => new Constructor(cons),
        PropertyInfo pr => new Property(pr),
        FieldInfo f => new Field(f),
        _ => null
    };

    private T? MaybeSplitInstance<T>(ref T[] args) where T:class {
        if (Static) return null;
        var inst = args[0];
        args = args.Skip(1).ToArray();
        return inst;
    }
    
    /// <summary>
    /// A <see cref="TypeMember"/> representing a class method.
    /// </summary>
    public class Method : TypeMember {
        /// <inheritdoc cref="BaseMi"/>
        public MethodInfo Mi { get; init; }
        /// <inheritdoc/>
        public override MemberInfo BaseMi => Mi;
        /// <inheritdoc/>
        public override NamedParam[] Params { get; }
        /// <inheritdoc/>
        public override Type ReturnType => Mi.ReturnType;
        /// <inheritdoc/>
        public override bool Static => Mi.IsStatic;
        /// <summary>
        /// True iff this is an extension method.
        /// </summary>
        public bool IsExtension { get; }

        /// <inheritdoc cref="TypeMember.Method"/>
        public Method(MethodInfo Mi) {
            this.Mi = Mi;
            Params = ParamsForMethod(Mi);
            IsExtension = Mi.GetCustomAttribute<ExtensionAttribute>() != null;
        }

        /// <summary>
        /// Converts the parameters of this method, including the instance if applicable, to
        /// an array of <see cref="NamedParam"/>.
        /// </summary>
        public static NamedParam[] ParamsForMethod(MethodInfo mi) {
            var args = mi.GetParameters().Select(x => (NamedParam)x);
            return (mi.IsStatic ? args : args.Prepend(new(mi.ReflectedType!, "Instance"))).ToArray();
        }

        /// <inheritdoc/>
        public override object? InvokeInst(object? instance, params object?[] args) => Mi.Invoke(instance, args);
        
        /// <inheritdoc/>
        public override Ex InvokeExInst(Ex? instance, params Ex[] args) => Ex.Call(instance, Mi, args);
        
        /// <inheritdoc/>
        public override string TypeOnlySignature() {
            if (Params.Length == 0 || Params.Length == 1 && !Static)
                return ReturnType.SimpRName();
            return $"({string.Join(", ", Params.Skip(Static ? 0 : 1).Select(p => p.Type.SimpRName()))}): {ReturnType.SimpRName()}";
        }

        /// <inheritdoc/>
        public override string AsSignature(Func<NamedParam, int, string> paramMod) => Static ?
            $"{ReturnType.SimpRName()} {Mi.Name}({string.Join(", ", Params.Select(paramMod))})" :
            $"{ReturnType.SimpRName()} {Params[0].Type.SimpRName()}.{Mi.Name}({string.Join(", ", Params.Select(paramMod).Skip(Static ? 0 : 1))})";
    }

    /// <summary>
    /// A <see cref="TypeMember"/> representing a constructor.
    /// </summary>
    public class Constructor : TypeMember {
        /// <inheritdoc cref="BaseMi"/>
        public ConstructorInfo Cons { get; init; }
        /// <inheritdoc/>
        public override MemberInfo BaseMi => Cons;
        /// <inheritdoc/>
        public override NamedParam[] Params { get; }
        /// <inheritdoc/>
        public override Type ReturnType => Cons.DeclaringType!;
        /// <inheritdoc/>
        public override bool Static => true;
        
        /// <inheritdoc cref="TypeMember.Constructor"/>
        public Constructor(ConstructorInfo Cons) {
            this.Cons = Cons;
            Params = Cons.GetParameters().Select(x => (NamedParam)x).ToArray();
        }

        /// <inheritdoc/>
        public override object InvokeInst(object? instance, params object?[] args) => Cons.Invoke(args);
        
        /// <inheritdoc/>
        public override Ex InvokeExInst(Ex? instance, params Ex[] args) => Ex.New(Cons, args);
        
        /// <inheritdoc/>
        public override string TypeOnlySignature() => Params.Length == 0 ? "" : 
                $"({string.Join(", ", Params.Select(p => p.Type.SimpRName()))})";

        /// <inheritdoc/>
        public override string AsSignature(Func<NamedParam, int, string> paramMod) =>
            $"new {TypeName}({string.Join(", ", Params.Select(paramMod))})";
    }

    /// <summary>
    /// Base class for <see cref="TypeMember"/>s that are writeable (ie. fields and properties).
    /// </summary>
    public abstract class Writeable : TypeMember {
        /// <summary>
        /// Set the value of this member.
        /// If this is an instance member, then `instance` must be provided as an instance
        ///  on which to invoke the member; if this is a static member, then `instance` must be null.
        /// </summary>
        public abstract void SetInst(object? instance, object value, params object?[] index);
    }
    
    /// <summary>
    /// A <see cref="TypeMember"/> representing a property.
    /// </summary>
    public class Property : Writeable {
        /// <inheritdoc cref="BaseMi"/>
        public PropertyInfo Prop { get; init; }
        /// <inheritdoc/>
        public override MemberInfo BaseMi => Prop;
        /// <inheritdoc/>
        public override NamedParam[] Params { get; }
        /// <inheritdoc/>
        public override Type ReturnType { get; }
        /// <inheritdoc/>
        public override bool Static { get; }
        
        /// <inheritdoc cref="TypeMember.Property"/>
        public Property(PropertyInfo Prop) {
            this.Prop = Prop;
            var getter = Prop.GetMethod!;
            Params = Method.ParamsForMethod(getter);
            ReturnType = getter.ReturnType;
            Static = getter.IsStatic;
        }

        /// <inheritdoc/>
        public override object? InvokeInst(object? instance, params object?[] index) =>
            index.Length > 0 ? Prop.GetValue(instance, index) : Prop.GetValue(instance);

        /// <inheritdoc/>
        public override void SetInst(object? instance, object value, params object?[] index) {
            if (index.Length > 0) {
                Prop.SetValue(instance, value, index);
            } else {
                Prop.SetValue(instance, value);
            }
        }

        /// <inheritdoc/>
        public override Ex InvokeExInst(Ex? instance, params Ex[] index) =>
            //these return different expressions, so we can't just use the first call
            index.Length > 0 ? Ex.Property(instance, Prop, index) : Ex.Property(instance, Prop);

        /// <inheritdoc/>
        public override string TypeOnlySignature() => ReturnType.SimpRName();
        
        /// <inheritdoc/>
        public override string AsSignature(Func<NamedParam, int, string> paramMod) =>
            $"{ReturnType.SimpRName()} {TypeName}.{Prop.Name}";
    }

    /// <summary>
    /// A <see cref="TypeMember"/> representing a field.
    /// </summary>
    public class Field : Writeable {
        /// <inheritdoc cref="BaseMi"/>
        public FieldInfo Fi { get; init; }
        /// <inheritdoc/>
        public override MemberInfo BaseMi => Fi;
        /// <inheritdoc/>
        public override NamedParam[] Params { get; }
        /// <inheritdoc/>
        public override Type ReturnType { get; }
        /// <inheritdoc/>
        public override bool Static { get; }
        
        /// <inheritdoc cref="TypeMember.Field"/>
        public Field(FieldInfo Fi) {
            this.Fi = Fi;
            Params = Fi.IsStatic ? 
                Array.Empty<NamedParam>() : 
                new[] { new NamedParam(Fi.ReflectedType!, "Instance") };
            ReturnType = Fi.FieldType;
            Static = Fi.IsStatic;
        }

        /// <inheritdoc/>
        public override object? InvokeInst(object? instance, params object?[] args) => Fi.GetValue(instance);
        
        /// <inheritdoc/>
        public override void SetInst(object? instance, object value, params object?[] args) => 
            Fi.SetValue(instance, value);
        
        /// <inheritdoc/>
        public override Ex InvokeExInst(Ex? instance, params Ex[] args) => Ex.Field(instance, Fi);

        /// <inheritdoc/>
        public override string TypeOnlySignature() => ReturnType.SimpRName();
        
        /// <inheritdoc/>
        public override string AsSignature(Func<NamedParam, int, string> paramMod) =>
            $"{ReturnType.SimpRName()} {TypeName}.{Fi.Name}";
    }

}
