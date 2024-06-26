﻿//Original implementation from FFTFlat https://github.com/sinshu/fftflat/tree/main, distributed under MIT.
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace BagoumLib.SignalProcessing;
internal static class ArrayMath {
    public static void MultiplyInplace(Span<double> x, double y) {
        var vectors = MemoryMarshal.Cast<double, Vector<double>>(x);

        var count = 0;

        for (var i = 0; i < vectors.Length; i++) {
            vectors[i] *= y;
            count += Vector<double>.Count;
        }

        for (var i = count; i < x.Length; i++) {
            x[i] *= y;
        }
    }
}