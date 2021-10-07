using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reflection;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Suzunoya;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;
using Suzunoya.Dialogue;

namespace Tests.Suzunoya {
public static class EventRecordHelpers {
    private static readonly Type tobs = typeof(IObservable<>);
    private static readonly MethodInfo registerEv = typeof(EventRecord).GetMethod("TrackEvent")!;
    private static HashSet<string> ignoreKeys = new() {"onupdate", "logs"};
    public static void Record(this EventRecord er, object obj) {
        foreach (var prop in obj.GetType().GetProperties().OrderBy(p => p.Name)) {
            foreach (var intf in prop.PropertyType.GetInterfaces()) {
                if (intf.IsConstructedGenericType && intf.GetGenericTypeDefinition() == tobs) {
                    if (ignoreKeys.Contains(prop.Name.ToLower())) continue;
                    var genType = intf.GenericTypeArguments[0];
                    registerEv.MakeGenericMethod(genType).Invoke(er, new[] {obj, prop.Name, prop.GetValue(obj)!});
                }
            }
        }
        if (obj is VNState vn) {
            vn.InterrogatorCreated.Subscribe(new AskRecv(er, vn));
            vn.EntityCreated.Subscribe(er.Record);
            vn.RenderGroupCreated.Subscribe(er.Record);
        }
    }

    public static List<EventRecord.LogEvent> GetAndClear(this EventRecord er) {
        var l = er.LoggedEvents.Published.ToList();
        er.LoggedEvents.Clear();
        return l;
    }

    private record AskRecv(EventRecord er, VNState vn) : IInterrogatorReceiver {
        public void OnNext<T>(IInterrogator<T> data) {
            er.LoggedEvents.OnNext(new EventRecord.LogEvent(vn, "Interrogator", typeof(IInterrogator), data));
        }
    }
}
}