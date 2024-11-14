using BagoumLib.Expressions;

namespace Scriptor.Expressions;
using Ex = System.Linq.Expressions.Expression;

/// <summary>
/// Extensions for TEx.
/// </summary>
public static class TExExtensions {
    /// <inheritdoc cref="ExExtensions.TryAsConst{T}"/>
    public static bool TryAsConst<T>(this TEx<T> tex, out T val) => ((Ex)tex).TryAsConst(out val);
    /// <inheritdoc cref="ExExtensions.Is"/>
    public static Ex Is<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Is(other);
    /// <inheritdoc cref="ExExtensions.Add(Ex,Ex)"/>
    public static Ex Add<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Add(other);
    /// <inheritdoc cref="ExExtensions.Mul(Ex,Ex)"/>
    public static Ex Mul<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Mul(other);
    /// <inheritdoc cref="ExExtensions.Add(Ex,float)"/>
    public static Ex Add<T>(this TEx<T> tex, float other) => ((Ex) tex).Add(other);
    /// <inheritdoc cref="ExExtensions.Sub(Ex,float)"/>
    public static Ex Sub<T>(this TEx<T> tex, float other) => ((Ex) tex).Sub(other);
    /// <inheritdoc cref="ExExtensions.Mul(Ex,float)"/>
    public static Ex Mul<T>(this TEx<T> tex, float other) => ((Ex) tex).Mul(other);
    /// <inheritdoc cref="ExExtensions.Sub(Ex,Ex)"/>
    public static Ex Sub<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Sub(other);
    /// <inheritdoc cref="ExExtensions.Div(Ex,Ex)"/>
    public static Ex Div<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Div(other);
    /// <inheritdoc cref="ExExtensions.LT"/>
    public static Ex LT<T>(this TEx<T> tex, Ex than) => ((Ex) tex).LT(than);
    /// <inheritdoc cref="Ex.LessThanOrEqual(Ex,Ex)"/>
    public static Ex Leq<T>(this TEx<T> tex, Ex than) => Ex.LessThanOrEqual(tex, than);
    /// <inheritdoc cref="ExExtensions.LT0"/>
    public static Ex LT0<T>(this TEx<T> tex) => ((Ex) tex).LT0();
    /// <inheritdoc cref="ExExtensions.GT"/>
    public static Ex GT<T>(this TEx<T> tex, Ex than) => ((Ex) tex).GT(than);
    /// <inheritdoc cref="ExExtensions.GT0"/>
    public static Ex GT0<T>(this TEx<T> tex) => ((Ex) tex).GT0();
    /// <inheritdoc cref="Ex.PropertyOrField"/>
    public static Ex Field<T>(this TEx<T> tex, string field) => Ex.PropertyOrField(tex, field);
}