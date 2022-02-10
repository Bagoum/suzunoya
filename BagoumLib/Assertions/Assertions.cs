using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib.Tasks;

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
    /// Called when assertion has been added with no preceding assertion.
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
    /// </summary>
    /// <param name="prev">Previous assertion</param>
    Task Inherit(IAssertion prev);
}
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

public interface IChildLinkedAssertion : IAssertion {
    public List<IAssertion> Children { get; }
}

public static class AssertionHelpers {
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
public record IdealizedState {
    private List<IAssertion> Assertions { get; } = new();
    private Dictionary<int, List<IAssertion>> AssertionsByPhase { get; } = new();
    public bool IsActualized { get; private set; } = false;

    public IdealizedState() {}
    public IdealizedState(params IAssertion[] assertions) {
        Assert(assertions);
    }
    public async Task Actualize(IdealizedState? prev) {
        if (prev is not { IsActualized: true })
            await ActualizeOnNewState();
        else {
            IsActualized = true;
            var tasks = new List<Task>();
            var typToAssertions = new Dictionary<Type, List<IAssertion>>(); //for new assertions
            var inheritMap = new Dictionary<IAssertion, IAssertion>(); //new -> prev
            foreach (var a in Assertions) {
                var t = a.GetType();
                if (!typToAssertions.TryGetValue(t, out var l))
                    typToAssertions[t] = l = new();
                l.Add(a);
            }
            foreach (var pg in prev.AssertionsByPhase) {
                foreach (var pa in pg.Value) {
                    var t = pa.GetType();
                    if (typToAssertions.TryGetValue(t, out var l))
                        for (int ii = 0; ii < l.Count; ++ii)
                            if (l[ii].ID == pa.ID) {
                                inheritMap[l[ii]] = pa;
                                l.RemoveAt(ii);
                                goto foundPair;
                            }
                    tasks.Add(pa.DeactualizeOnNoSucceeding());
                    foundPair: ;
                }
                Logging.Log($"Deactualizing previous state for phase {pg.Key}...");
                if (tasks.Count > 0)
                    await Task.WhenAll(tasks);
                Logging.Log($"Completed deactualization of previous for phase {pg.Key}");
                tasks.Clear();
            }
            foreach (var g in AssertionsByPhase) {
                foreach (var a in g.Value) {
                    if (inheritMap.TryGetValue(a, out var pa))
                        tasks.Add(a.Inherit(pa));
                    else
                        tasks.Add(a.ActualizeOnNoPreceding());
                }
                Logging.Log($"Actualizing next state for phase {g.Key}...");
                await Task.WhenAll(tasks);
                Logging.Log($"Completed actualization of next state for phase {g.Key}");
                tasks.Clear();
            }
        }
    }

    public virtual async Task ActualizeOnNewState() {
        IsActualized = true;
        foreach (var phase in AssertionsByPhase.Keys.OrderBy(x => x)) {
            Logging.Log($"Actualizing new-state for phase {phase}...");
            await Task.WhenAll(AssertionsByPhase[phase].Select(a => a.ActualizeOnNewState()));
            Logging.Log($"Completed actualization of new-state for phase {phase}");
        }
    }
    public virtual async Task DeactualizeOnEndState() {
        IsActualized = false;
        foreach (var phase in AssertionsByPhase.Keys.OrderBy(x => x)) {
            Logging.Log($"Deactualizing end-state for phase {phase}...");
            await Task.WhenAll(AssertionsByPhase[phase].Select(a => a.DeactualizeOnEndState()));
            Logging.Log($"Completed deactualization of end-state for phase {phase}");
        }
    }

    public void Assert(params IAssertion[] assertions) {
        var reorderPhases = new HashSet<int>();
        void HandleAssertion(IAssertion a) {
            Assertions.AddRange(assertions);
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