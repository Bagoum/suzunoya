using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Suzunoya;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;
using Suzunoya.Dialogue;

namespace Tests.Suzunoya {
public static class EventRecordHelpers {
    private static readonly HashSet<Type> tObs = new() { typeof(IObservable<>), typeof(ICObservable<>) };
    private static readonly MethodInfo registerEv = typeof(EventRecord).GetMethod("TrackEvent")!;
    private static HashSet<string> ignoreKeys = new() {
        "onupdate", "logs", 
        //use computed instead
        "location", "euleranglesd", "scale", "tint"
    };

    private static IEnumerable<(PropertyInfo, Type)> EvPropsPerType(Type typ) {
        var viewedNames = new HashSet<string>();
        foreach (var prop in typ.GetInterfaces().Append(typ)
            .SelectMany(t => t.GetProperties())
            .OrderBy(p => p.Name.StartsWith("Computed") ? p.Name[8..] : p.Name)) {
            if (ignoreKeys.Contains(prop.Name.ToLower())) continue;
            if (viewedNames.Contains(prop.Name)) continue;
            viewedNames.Add(prop.Name);
            foreach (var intf in prop.PropertyType.GetInterfaces().Append(prop.PropertyType)) {
                if (intf.IsConstructedGenericType && tObs.Contains(intf.GetGenericTypeDefinition())) {
                    yield return (prop, intf.GenericTypeArguments[0]);
                    break;
                }
            }
        }
    }
    public static void Record(this EventRecord er, object obj) {
        foreach (var (prop, genType) in EvPropsPerType(obj.GetType())) {
            registerEv.MakeGenericMethod(genType).Invoke(er, new[] {obj, prop.Name, prop.GetValue(obj)!});
        }
        if (obj is VNState vn) {
            vn.EntityCreated.Subscribe(er.Record);
            vn.RenderGroupCreated.Subscribe(er.Record);
        }
    }

    public static List<EventRecord.LogEvent> GetAndClear(this EventRecord er) {
        var l = er.LoggedEvents.Published.ToList();
        er.LoggedEvents.Clear();
        return l;
    }
}
}