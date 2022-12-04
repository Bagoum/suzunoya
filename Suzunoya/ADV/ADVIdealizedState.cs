using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using JetBrains.Annotations;
using Suzunoya.Assertions;
using Suzunoya.ControlFlow;

namespace Suzunoya.ADV {
/// <summary>
/// Enum describing the reason for which a VN segment should be run on entry.
/// </summary>
public enum RunOnEntryVNPriority {
    /// <summary>
    /// Run a VN segment on entry because the game was saved while that segment was running.
    /// </summary>
    LOAD = 0,
    /// <summary>
    /// Run a VN segment on entry because the map configuration says so.
    /// </summary>
    MAP_ENTER = 1
}

/// <summary>
/// A set of assertions about the state of an ADV game.
/// </summary>
[PublicAPI]
public record ADVIdealizedState : IdealizedState {
    private readonly IExecutingADV inst;
    private (BoundedContext<Unit> bctx, RunOnEntryVNPriority priority)? runOnEnterVN;
    /// <summary>
    /// Whether or not this idealized state has a VN segment it will run on actualization.
    /// </summary>
    public bool HasEntryVN => runOnEnterVN != null;
    
    /// <summary>
    /// Constructor for <see cref="ADVIdealizedState"/> that starts with no assertions.
    /// <br/>Assertions can be added via <see cref="IdealizedState.Assert(IAssertion[])"/>.
    /// </summary>
    public ADVIdealizedState(IExecutingADV inst) {
        this.inst = inst;
        Assert(new RunOnEntryAssertion(EntryTask) {
            Priority = (int.MaxValue, int.MaxValue)
        });
    }

    /// <summary>
    /// Set a visual novel segment to be run when this idealized state is actualized.
    /// </summary>
    public bool SetEntryVN(BoundedContext<Unit> bctx, RunOnEntryVNPriority priority = RunOnEntryVNPriority.MAP_ENTER) {
        if (runOnEnterVN == null || runOnEnterVN?.priority > priority) {
            runOnEnterVN = (bctx, priority);
            return true;
        }
        return false;
    }

    private Task EntryTask() {
        if (runOnEnterVN.Try(out var x)) {
            //Don't await this! We only want to start the bctx--
            // it will be completed during normal play.
            _ = inst.Manager.ExecuteVN(x.bctx);
        }
        return Task.CompletedTask;
    }
    
    //TODO: extend these with orderings for background, music, etc.
    /// <inheritdoc/>
    public override async Task ActualizeOnNewState() {
        await base.ActualizeOnNewState();
        await FadeIn();
    }
    /// <inheritdoc/>
    public override async Task DeactualizeOnEndState() {
        await FadeOut();
        await base.DeactualizeOnEndState();
        inst.VN.MainDialogue?.Clear();
    }

    /// <summary>
    /// A task that is run after <see cref="ActualizeOnNewState"/>.
    /// </summary>
    protected virtual Task FadeIn() => Task.CompletedTask;
    
    /// <summary>
    /// A task that is run before <see cref="DeactualizeOnEndState"/>.
    /// </summary>
    protected virtual Task FadeOut() => Task.CompletedTask;
}
}