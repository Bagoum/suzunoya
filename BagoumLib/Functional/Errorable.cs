using System;

namespace BagoumLib.Functional {

public readonly struct Errorable<T> {
    public readonly string[] errors;
    public string JoinedErrors => string.Join("\n", errors);
    private readonly Maybe<T> _value;
    public bool Valid => _value.Valid;
    public T Value => _value.Value;
    public T GetOrThrow => Valid ? Value : throw new Exception(JoinedErrors);
    public Either<T, string> AsEither => Valid ? new Either<T, string>(Value) : new Either<T, string>(JoinedErrors);
    private Errorable(string[]? errors, Maybe<T> value) {
        this.errors = errors ?? Helpers.noStrs;
        this._value = value;
    }
    public static Errorable<T> Fail(string[] errs) => new(errs, Maybe<T>.None);
    public static Errorable<T> Fail(string err) => new(new[] {err}, Maybe<T>.None);
    public static Errorable<T> OK(T value) => new(null, Maybe<T>.Of(value));

    public static implicit operator Errorable<T>(T value) => OK(value);

    public Errorable<U> Map<U>(Func<T, U> f) => Valid ? Errorable<U>.OK(f(Value)) : Errorable<U>.Fail(errors);
    public Errorable<U> Bind<U>(Func<T, Errorable<U>> f) => Valid ? f(Value) : Errorable<U>.Fail(errors);
}
}