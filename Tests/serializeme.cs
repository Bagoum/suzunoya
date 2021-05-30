using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
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
            ReadLines = new Dictionary<string, int>() {
                {"script1", 233}
            }
        };
        var save = new InstanceData() {
            Data = new Dictionary<string, object>() {
                {"hello", new[] {"w", "orld"}},
                {"foo", 433}
            },
            Location = new List<(string, int)>() {("script1", 123)},
            GlobalData = global
        };

        var typs = new JsonSerializerSettings() {TypeNameHandling = TypeNameHandling.Auto};
        string s_save = JsonConvert.SerializeObject(save, Formatting.Indented, typs);
        var r_save = JsonConvert.DeserializeObject<InstanceData>(s_save, typs);
        string s_global = JsonConvert.SerializeObject(global, Formatting.Indented, typs);
        var r_global = JsonConvert.DeserializeObject<GlobalData>(s_global, typs);
        
    }
}
}