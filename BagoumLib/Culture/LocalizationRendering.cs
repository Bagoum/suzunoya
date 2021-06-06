namespace BagoumLib.Culture {
public static class LocalizationRendering {
    public static string Render(string? locale, string[] pieces, params object[] fmtArgs) {
        for (int ii = 0; ii < fmtArgs.Length; ++ii) {
            if (fmtArgs[ii] is IVariant v) {
                fmtArgs[ii] = v.RealizeObj(locale);
            }
        }
        if (pieces.Length == 1) {
            if (fmtArgs.Length == 0)
                return pieces[0];
            else
                return string.Format(pieces[0], fmtArgs);
        } else
            return string.Format(string.Join("", pieces), fmtArgs);
    }
}
}