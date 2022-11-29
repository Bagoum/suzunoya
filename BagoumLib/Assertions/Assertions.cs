using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib.Tasks;
using JetBrains.Annotations;

namespace BagoumLib.Assertions {

/// <summary>
/// A statement about the ideal status of an object in a <see cref="IdealizedState"/>,
///  providing functions that create, destroy, or modify the object as necessary.
/// </summary>
public interface IAssertion {
    /// <summary>
    /// String that uniquely describes this assertion.
    /// <br/>This is used to pair old assertions to new assertions when the idealized state changes.
    /// <br/>Not required if all assertions have unique types.
    /// </summary>
    string? ID => null;
    
    /// <summary>
    /// Ordering priority for assertions. Lower priority assertions are executed first.
    /// <br/>Assertions are executed by phase, so all assertions in phase 0 are executed, then in phase 1, etc.
    /// </summary>
    (int Phase, int Ordering) Priority => (0, 0);

    /// <summary>
    /// Called when the state is first being actualized.
    /// </summary>
    Task ActualizeOnNewState();

    /// <summary>
    /// Called when this assertion has been added with no preceding assertion.
    /// </summary>
    Task ActualizeOnNoPreceding();
    
    /// <summary>
    /// Called when the state is being deactualized.
    /// </summary>
    Task DeactualizeOnEndState();

    /// <summary>
    /// Called when this assertion has been deleted with no succeeding assertion.
    /// </summary>
    Task DeactualizeOnNoSucceeding();
    
    /// <summary>
    /// Called when a previous assertion is replaced with this one.
    /// <br/>You should generally implement this as AssertionHelpers.Inherit(prev, this).
    /// </summary>
    /// <param name="prev">Previous assertion</param>
    Task Inherit(IAssertion prev);
}

/// <summary>
/// An <see cref="IAssertion"/> restricted to the type of the designated object.
/// </summary>
public interface IAssertion<T> : IAssertion where T: IAssertion<T> {
    /// <summary>
    /// Called when a previous assertion is replaced with this one.
    /// </summary>
    /// <param name="prev">Previous assertion</param>
    Task _Inherit(T prev);
    
    //Note: while it is possible to define a default implementation of IAssertion.Inherit here,
    // for some reason that causes untraceable stack overflow bugs in Unity that may or may not be my fault,
    // so I've opted for just having an explicit implementation in each class.
    //Just use:
    //   public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
}

/// <summary>
/// An assertion with children, which may be defined to have some special relation to the parent.
/// For example, an EntityAssertion's children's transforms are children of that EntityAssertion's transform.
/// </summary>
public interface IChildLinkedAssertion : IAssertion {
    /// <summary>
    /// Child assertions.
    /// </summary>
    public List<IAssertion> Children { get; }
}

/// <summary>
/// Static class providing helpers for assertions
/// </summary>
public static class AssertionHelpers {
    /// <summary>
    /// Convert the untyped assertion `prev` to type IAssertion{T}, then inherit it.
    /// </summary>
    public static Task Inherit<T>(IAssertion prev, IAssertion<T> thisNext) where T: IAssertion<T> {
        return thisNext._Inherit(prev is T obj ?
            obj :
            throw new Exception(
                $"Couldn't inherit assertion of type {prev.GetType()} for new assertion of type {typeof(T)}"));
    }
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
    /// <param name="simultaneousActualize">When true, the tasks for deactualizing the previous state
    /// and actualizing the new state will be executed simultaneously.</param>
    public async Task Actualize(IdealizedState? prev, bool simultaneousActualize = false) {
        if (prev is not { IsActualized: true })
            await ActualizeOnNewState();
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
                            if (l[ii].ID == pa.ID) {
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
                    Logging.Log($"Deactualizing previous state for phase {phase}...");
                    await Task.WhenAll(assertions.Select(pa => pa.DeactualizeOnNoSucceeding()));
                    Logging.Log($"Completed deactualization of previous for phase {phase}");
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
                        Logging.Log($"Actualizing next state for phase {phase}...");
                        await Task.WhenAll(tasks);
                        Logging.Log($"Completed actualization of next state for phase {phase}");
                    }
                    pairings.Clear();
                    tasks.Clear();
                }
            }

            if (simultaneousActualize)
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
    public virtual async Task ActualizeOnNewState() {
        IsActualized = true;
        foreach (var (phase, assertions) in AssertionsByPhase.OrderBy(p => p.Key)) {
            Logging.Log($"Actualizing new-state for phase {phase}...");
            await Task.WhenAll(assertions.Select(a => a.ActualizeOnNewState()));
            Logging.Log($"Completed actualization of new-state for phase {phase}");
        }
    }
    
    /// <summary>
    /// Deactualize this state when no next state exists (ie, this is the last state).
    /// If this is not the last state, then call nextState.Actualize(thisState),
    ///  which will deactualize thisState.
    /// </summary>
    public virtual async Task DeactualizeOnEndState() {
        IsActualized = false;
        foreach (var (phase, assertions) in AssertionsByPhase.OrderBy(p => p.Key)) {
            Logging.Log($"Deactualizing end-state for phase {phase}...");
            await Task.WhenAll(assertions.Select(a => a.DeactualizeOnEndState()));
            Logging.Log($"Completed deactualization of end-state for phase {phase}");
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


}