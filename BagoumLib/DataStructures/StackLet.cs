using System;
using System.Collections.Generic;

namespace BagoumLib.DataStructures;

/// <summary>
/// Wrapper around Stack.Push to allow `using`/Dispose pattern.
/// </summary>
public class StackLet<T>(Stack<T> Data): IDisposable {
    public StackLet(Stack<T> data, T entry) : this(data) {
        data.Push(entry);
    }

    public void Dispose() {
        Data.Pop();
    }
}