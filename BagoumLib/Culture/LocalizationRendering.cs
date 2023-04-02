using JetBrains.Annotations;

namespace BagoumLib.Culture {
/// <summary>
/// Helpers for rendering localization strings.
/// </summary>
[PublicAPI]
public static class LocalizationRendering {
    /// <summary>
    /// For each argument, if it is <see cref="IVariant"/>, then realize it with the provided locale.
    /// </summary>
    public static void FormatArgs(string? locale, params object[] fmtArgs) {
        for (int ii = 0; ii < fmtArgs.Length; ++ii) {
            if (fmtArgs[ii] is IVariant v) {
                fmtArgs[ii] = v.RealizeObj(locale);
            }
        }
    }
    
    /// <summary>
    /// String-format the strings in `pieces` using the values in `fmtArgs`, which may be locale-dependent,
    ///  and then join them.
    /// </summary>
    /// <example>
    /// Render("en", new[]{"{0}", " and", " {1}"}, IVariant{"en"=>"A"}, IVariant{"en"=>"B"})
    ///  = "A and B"
    /// </example>
    /// <param name="locale"></param>
    /// <param name="fmtStrings">Strings to format.</param>
    /// <param name="fmtArgs">Arguments to string formatting, which may be <see cref="IVariant"/>.</param>
    public static string Render(string? locale, string[] fmtStrings, params object[] fmtArgs) {
        if (fmtStrings.Length == 1)
            return Render(locale, fmtStrings[0], fmtArgs);
        FormatArgs(locale, fmtArgs);
        return string.Format(string.Join("", fmtStrings), fmtArgs);
    }
    
    /// <inheritdoc cref="Render(string?,string[],object[])"/>
    public static string Render(string? locale, string fmtString, params object[] fmtArgs) {
        FormatArgs(locale, fmtArgs);
        if (fmtArgs.Length == 0)
            return fmtString;
        FormatArgs(locale, fmtArgs);
        return string.Format(fmtString, fmtArgs);
    }
}
}