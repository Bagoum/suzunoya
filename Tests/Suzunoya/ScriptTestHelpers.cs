using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using Suzunoya;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;
using Suzunoya.Dialogue;

namespace Tests.Suzunoya {
public static class ScriptTestHelpers {
    public static SpeechFragment c(char c) => new SpeechFragment.Char(c);

    public static List<(SpeechFragment, string)> FragmentsFromWord(string word) {
        var sfs = new List<(SpeechFragment, string)>();
        for (int ii = 0; ii < word.Length; ++ii)
            sfs.Add((new SpeechFragment.Char(word[ii]), word.Substring(ii + 1)));
        return sfs;
    }

    public static IEnumerable<EventRecord.LogEvent> DialogueEventsFromWord(this DialogueBox d, string word) =>
        FragmentsFromWord(word).Select(f => new EventRecord.LogEvent(d, "Dialogue", f));
}

public class TestScript {
    public readonly VNState vn;
    public readonly EventRecord er;
    protected readonly Cancellable cTs = new Cancellable();

    public TestScript(VNState? vn = null) {
        this.vn = vn ?? new VNState(cTs);
        er = new EventRecord();
        er.Record(this.vn);
    }
    
    public EventRecord.LogEvent UpdateLog(int ii) => new(vn, "$UpdateCount", typeof(int), ii);
}

}