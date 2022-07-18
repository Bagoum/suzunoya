using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using BagoumLib.Events;
using BagoumLib.Functional;
using Newtonsoft.Json;
using NUnit.Framework;
using Suzunoya.ControlFlow;
using Suzunoya.Data;

namespace Tests {
public class serializeme {
    [Serializable]
    public record MyClass {
        public int X { get; init; }
        public string Y { get; init; }
        public Dictionary<string, int[]> D { get; init; }
    }
    [Test]
    public void TestSerialize() {
        var dict = new Dictionary<string, object>();
        dict["a"] = 5;
        dict["b"] = "hello world";
        dict["c"] = new MyClass() {
            X = 12,
            Y = "foo bar",
            D = new Dictionary<string, int[]>() {
                {"1", new[] {10, 11, 12}},
                {"2", new[] {20, 21, 22}},
                {"3", new[] {30, 31, 32}},
            }
        };

        var global = new GlobalData() {
            Settings = new Settings() {
                TextSpeed = 1.5f
            },
            ReadLines = new HashSet<string>() {
                "233", "453"
            }
        };
        var bctxc = new BoundedContextData<int>("c", 5, new(), new());
        bctxc.Locals.SaveData("hello", 10);
        var bctxb = new BoundedContextData<Unit>("b", Unit.Default, new(), new());
        bctxb.SaveNested(bctxc);
        var bctxa = new BoundedContextData<Unit>("a", Maybe<Unit>.None, new(), new());
        bctxa.SaveNested(bctxb);
        bctxa.Locals.SaveData("world", "foobar");
        var save = new InstanceData(global) {
            BCtxData = new() {
                {"a", bctxa}
            },
            Location = new VNLocation("l_25", new List<string>(){"dec20"}) {
            }
        };
        global.Settings.TextSpeed = 2;
        Assert.AreEqual(save.FrozenGlobalData.Settings.TextSpeed, 1.5f);
        Assert.AreEqual(save.GlobalData.Settings.TextSpeed, 2f);

        var typs = new JsonSerializerSettings() {TypeNameHandling = TypeNameHandling.Auto};
        //pretend save
        string s_global = JsonConvert.SerializeObject(global, Formatting.Indented, typs);
        string s_save = JsonConvert.SerializeObject(save, Formatting.Indented, typs);
        //pretend load
        var r_global = JsonConvert.DeserializeObject<GlobalData>(s_global, typs) ?? throw new Exception();
        var r_save = InstanceData.Deserialize<InstanceData>(s_save, r_global);
        Assert.AreEqual(r_save.FrozenGlobalData.Settings.TextSpeed, 1.5f);
        Assert.AreEqual(r_global.Settings.TextSpeed, 2f);
        Assert.AreEqual(r_save.GlobalData, r_global);

        int k = 5;
    }

    [Serializable]
    public record MyClassWEvented {
        public Evented<int> X = new(5);
        public Evented<string> Y = new("hello");
    }
    [Test]
    public void TestCustomEventedSerialize() {
        var w = new MyClassWEvented();
        var typs = new JsonSerializerSettings() {TypeNameHandling = TypeNameHandling.Auto};
        string s1 = JsonConvert.SerializeObject(w, Formatting.None, typs);
        Assert.AreEqual(s1, "{\"X\":5,\"Y\":\"hello\"}");
        w.X.Value = 9;
        w.Y.Value = "world";
        string s2 = JsonConvert.SerializeObject(w, Formatting.None, typs);
        Assert.AreEqual(s2, "{\"X\":9,\"Y\":\"world\"}");
        var wd = JsonConvert.DeserializeObject<MyClassWEvented>(s2);
        w.X.Value = 12;
        w.Y.Value = "!!!";
        Assert.AreEqual(wd.X.Value, 9);
        Assert.AreEqual(wd.Y.Value, "world");

    }
}
}