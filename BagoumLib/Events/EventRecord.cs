using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Reflection;

namespace BagoumLib.Events {
public class EventRecord {
    public record LogEvent(object controller, string prop, Type propType, object? value) {
        public override string ToString() =>
            $"<{controller.GetType().RName()}>.{prop}<{propType.RName()}> ~ {value}<{value?.GetType().RName() ?? "Null"}>";
        
        public string ToSimpleString() =>
            $"<{controller.GetType().RName()}>.{prop} ~ {value}";
        

        public LogEvent(object controller, string prop, object value) : 
            this(controller, prop, controller.GetType().PropertyInfo(prop).PropertyType.GenericTypeArguments[0], value) { }
    }
    
    private readonly List<IDisposable> tokens = new();
    public AccEvent<LogEvent> LoggedEvents { get; } = new();

    public List<string> SimpleLoggedEventStrings => LoggedEvents.Published.Select(x => x.ToSimpleString()).ToList();
    public List<string> LoggedEventStrings => LoggedEvents.Published.Select(x => x.ToString()).ToList();

    public string CompileToTest() =>
        string.Join(",\n\t\t", SimpleLoggedEventStrings.Select(x => x.ToString().ToLiteral()));

    /*public string CompileToTest(Dictionary<object, string> varMapper, Func<object, string> valueMapper, int indent = 8) =>
        string.Join(",\n".PadRight(1 + indent),
            LoggedEvents.Published.Select(x => $"new({varMapper[x.controller]}, \"{x.prop}\""));*/

    public void TrackEventByName<T>(object controller, string prop) =>
        TrackEvent(controller, prop, controller._Property<IObservable<T>>(prop));

    public void TrackEvent<T>(object controller, string prop, IObservable<T> ev) {
        tokens.Add(ev.Subscribe(v => LoggedEvents.OnNext(new LogEvent(controller, prop, typeof(T), v!))));
    }

    public void Close() {
        foreach (var t in tokens)
            t.Dispose();
        tokens.Clear();
        LoggedEvents.Clear();
        LoggedEvents.OnCompleted();
    }
}
}