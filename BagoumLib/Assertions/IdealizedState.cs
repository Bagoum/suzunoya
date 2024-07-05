using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace BagoumLib.Assertions;

/// <summary>
/// Options to configure actualization/deactualization of <see cref="IdealizedState"/>.
/// </summary>
public record ActualizeOptions {
    /// <summary>
    /// Whether or not previous state deactualization should occur at the same time as next state actualization.
    /// </summary>
    public bool SimultaneousActualization { get; init; } = false;
    
    /// <summary>
    /// Whether or not this is the first state of any map being actualized.
    /// </summary>
    public bool ActualizeFromNull { get; internal set; }

    /// <summary>
    /// Default options (no simultaneous actualization).
    /// </summary>
    public static ActualizeOptions Default { get; } = new();

    /// <summary>
    /// Default options with simultaneous actualization.
    /// </summary>
    public static ActualizeOptions Simultaneous { get; } = new() { SimultaneousActualization = true };
}

/// <summary>
/// A set of assertions representing what the state of the world ought to be.
/// <br/>At any time, the state may be actualized (constructing objects represented by the state),
/// inherited (modifying objects already existing according to a new state representation),
/// or deactualized (destroying objects represented by the state).
/// </summary>
[PublicAPI]
public record IdealizedState {
    private List<IAssertion> Assertions { get; } = new();
    private Dictionary<int, List<IAssertion>> AssertionsByPhase { get; } = new();
    /// <summary>
    /// Whether or not the state is currently actualized.
    /// </summary>
    public bool IsActualized { get; private set; } = false;

    /// <summary>
    /// Create an idealized state with no assertions.
    /// </summary>
    public IdealizedState() {}
    
    /// <summary>
    /// Create an idealized state with some assertions.
    /// </summary>
    public IdealizedState(params IAssertion[] assertions) {
        Assert(assertions);
    }
    
    /// <summary>
    /// Deactualize the previous state, then actualize this state,
    ///  creating the objects designated by this object's assertions.
    /// </summary>
    /// <param name="prev">A previous state. Objects will be transferred instead of created if already created
    /// by the previous state.</param>
    /// <param name="opts">Options for configuring actualization.</param>
    public async Task Actualize(IdealizedState? prev, ActualizeOptions opts) {
        if (prev is not { IsActualized: true })
            await ActualizeOnNewState(opts with { ActualizeFromNull = true });
        else {
            IsActualized = true;
            var typToAssertions = new Dictionary<Type, List<IAssertion>>(); //for new assertions
            var phaseToDeactualizers = new Dictionary<int, List<IAssertion>>();
            var inheritMap = new Dictionary<IAssertion, IAssertion>(); //new -> prev
            foreach (var a in Assertions) {
                var t = a.GetType();
                if (!typToAssertions.TryGetValue(t, out var l))
                    typToAssertions[t] = l = new();
                l.Add(a);
            }
            foreach (var (phase, assertions) in prev.AssertionsByPhase.OrderBy(p => p.Key)) {
                foreach (var pa in assertions) {
                    var t = pa.GetType();
                    if (typToAssertions.TryGetValue(t, out var l))
                        for (int ii = 0; ii < l.Count; ++ii)
                            if (l[ii].ID == pa.ID && l[ii].Priority == pa.Priority) {
                                inheritMap[l[ii]] = pa;
                                l.RemoveAt(ii);
                                goto foundPair;
                            }
                    if (!phaseToDeactualizers.TryGetValue(phase, out l))
                        phaseToDeactualizers[phase] = l = new();
                    l.Add(pa);
                    foundPair: ;
                }
            }

            async Task DeactualizePrev() {
                foreach (var (phase, assertions) in phaseToDeactualizers.OrderBy(p => p.Key)) {
                    Logging.Logs.Log("Deactualizing previous state for phase {0}...", phase, level: LogLevel.DEBUG2);
                    await Task.WhenAll(assertions.Select(pa => pa.DeactualizeOnNoSucceeding()));
                    Logging.Logs.Log("Completed deactualization of previous for phase {0}", phase, level: LogLevel.DEBUG2);
                }
            }
            async Task ActualizeNext() {
                var tasks = new List<Task>();
                var pairings = new List<(IAssertion, IAssertion?)>();
                foreach (var (phase, assertions) in AssertionsByPhase.OrderBy(p => p.Key)) {
                    //Can't call Inherit directly in the loop as that can cause the hashcode to change
                    // (record-type equality semantics lmao)
                    // and destabilize the dictionary.
                    foreach (var a in assertions) 
                        pairings.Add(inheritMap.TryGetValue(a, out var pa) ? (a, pa) : (a, null));
                    foreach (var (a, pa) in pairings)
                        tasks.Add(pa == null ? a.ActualizeOnNoPreceding() : a.Inherit(pa));
                    if (tasks.Count > 0) {
                        Logging.Logs.Log("Actualizing next state for phase {0}...", phase, level: LogLevel.DEBUG2);
                        await Task.WhenAll(tasks);
                        Logging.Logs.Log("Completed actualization of next state for phase {0}", phase, level: LogLevel.DEBUG2);
                    }
                    pairings.Clear();
                    tasks.Clear();
                }
            }

            if (opts.SimultaneousActualization)
                await Task.WhenAll(DeactualizePrev(), ActualizeNext());
            else {
                await DeactualizePrev();
                await ActualizeNext();
            }
        }
    }

    /// <summary>
    /// Actualize this state when no previous state exists (ie, this is a new state).
    /// </summary>
    public virtual async Task ActualizeOnNewState(ActualizeOptions options) {
        IsActualized = true;
        foreach (var (phase, assertions) in AssertionsByPhase.OrderBy(p => p.Key)) {
            Logging.Logs.Log("Actualizing new-state for phase {0}...", phase, level: LogLevel.DEBUG2);
            await Task.WhenAll(assertions.Select(a => a.ActualizeOnNewState()));
            Logging.Logs.Log("Completed actualization of new-state for phase {0}", phase, level: LogLevel.DEBUG2);
        }
    }
    
    /// <summary>
    /// Deactualize this state when no next state exists (ie, this is the last state).
    /// If this is not the last state, then call nextState.Actualize(thisState),
    ///  which will deactualize thisState.
    /// </summary>
    public virtual async Task DeactualizeOnEndState(ActualizeOptions options) {
        IsActualized = false;
        foreach (var (phase, assertions) in AssertionsByPhase.OrderBy(p => p.Key)) {
            Logging.Logs.Log("Deactualizing end-state for phase {0}...", phase, level: LogLevel.DEBUG2);
            await Task.WhenAll(assertions.Select(a => a.DeactualizeOnEndState()));
            Logging.Logs.Log("Completed deactualization of end-state for phase {0}", phase, level: LogLevel.DEBUG2);
        }
    }

    /// <summary>
    /// Add assertions.
    /// </summary>
    public void Assert(params IAssertion[] assertions) => this.Assert(assertions as IEnumerable<IAssertion>);

    /// <summary>
    /// Add assertions.
    /// </summary>
    public void Assert(IEnumerable<IAssertion> assertions) {
        var reorderPhases = new HashSet<int>();
        void HandleAssertion(IAssertion a) {
            Assertions.Add(a);
            if (!AssertionsByPhase.TryGetValue(a.Priority.Phase, out var l))
                AssertionsByPhase[a.Priority.Phase] = l = new();
            l.Add(a);
            reorderPhases.Add(a.Priority.Phase);
            if (a is IChildLinkedAssertion pa)
                foreach (var c in pa.Children)
                    HandleAssertion(c);
        }
        
        foreach (var a in assertions)
            HandleAssertion(a);
        
        Assertions.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        foreach (var p in reorderPhases)
            AssertionsByPhase[p].Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }
}

