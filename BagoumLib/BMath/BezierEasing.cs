/* Original implementation in Javascript by Gaëtan Renaudeau at https://github.com/gre/bezier-easing/ 
 *
Copyright (c) 2014 Gaëtan Renaudeau

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
 *
 */

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace BagoumLib.Mathematics {
/// <summary>
/// Implementation of Bezier curves.
/// </summary>
[PublicAPI]
public static class Bezier {
    private const int NEWTON_ITERATIONS = 4;
    private const double NEWTON_MIN_SLOPE = 0.001;
    private const double SUBDIVISION_PRECISION = 0.0000001;
    private const double SUBDIVISION_MAX_ITERATIONS = 10;

    private const int kSplineTableSize = 11;
    private const double kSampleStepSize = 1.0 / (kSplineTableSize - 1.0);
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double A(double c1, double c2) => 1 - 3 * c2 + 3 * c1;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double B(double c1, double c2) => 3 * c2 - 6 * c1;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double C(double c1) => 3 * c1;

    /// <summary>
    /// Cubic bezier implementation in one dimension.
    /// </summary>
    /// <param name="t">Time [0, 1]</param>
    /// <param name="c1">First control point</param>
    /// <param name="c2">Second control point</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalcBezier(double t, double c1, double c2) =>
        ((A(c1, c2) * t + B(c1, c2)) * t + C(c1)) * t;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalcBezierDerivative(double t, double c1, double c2) =>
        3 * A(c1, c2) * t * t + 2 * B(c1, c2) * t + C(c1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double BinarySubdivide(double x, double a, double b, double x1, double x2) {
        double currx;
        double currt;
        var ii = 1;
        do {
            currt = a + (b - a) * 0.5;
            currx = CalcBezier(currt, x1, x2) - x;
            if (currx > 0)
                b = currt;
            else
                a = currt;
        } while (Math.Abs(currx) > SUBDIVISION_PRECISION && ++ii < SUBDIVISION_MAX_ITERATIONS);
        return currt;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double NewtonRaphsonIterate(double x, double guessT, double x1, double x2) {
        for (var ii = 0; ii < NEWTON_ITERATIONS; ++ii) {
            var currSlope = CalcBezierDerivative(guessT, x1, x2);
            if (currSlope == 0.0)
                return guessT;
            guessT -= (CalcBezier(guessT, x1, x2) - x) / currSlope;
        }
        return guessT;
    }

    /// <summary>
    /// Create a bezier easing function given two time-progression pairs.
    /// <br/>Works like cubic-bezier in CSS.
    /// </summary>
    /// <param name="t1">Time of first control point</param>
    /// <param name="p1">Progression of first control point</param>
    /// <param name="t2">Time of second control point</param>
    /// <param name="p2">Progression of second control point</param>
    /// <returns></returns>
    public static Easer CBezier(double t1, double p1, double t2, double p2) {
        var samples = new double[kSplineTableSize];
        for (int ii = 0; ii < kSplineTableSize; ++ii) {
            samples[ii] = CalcBezier(ii * kSampleStepSize, t1, t2);
        }
        return x => (float)CalcBezier(GetTForX(x, samples, t1, t2), p1, p2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetTForX(double x, double[] samples, double x1, double x2) {
        var intervalStart = 0.0;
        int currSample = 1;
        for (; currSample < kSplineTableSize - 1 && samples[currSample] <= x; ++currSample) {
            intervalStart += kSampleStepSize;
        }
        --currSample;
        
        // Interpolate to provide an initial guess for t
        var dist = (x - samples[currSample]) / (samples[currSample + 1] - samples[currSample]);
        var guessT = intervalStart + dist * kSampleStepSize;

        var slope0 = CalcBezierDerivative(guessT, x1, x2);
        if (slope0 >= NEWTON_MIN_SLOPE)
            return NewtonRaphsonIterate(x, guessT, x1, x2);
        else if (slope0 == 0.0)
            return guessT;
        else
            return BinarySubdivide(x, intervalStart, intervalStart + kSampleStepSize, x1, x2);
    }


}
}
