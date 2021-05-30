using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Tasks;
using BagoumLib.Tweening;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Display;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {

public interface IVNState {
    ICancellee CToken { get; }
    /// <summary>
    /// Within the update loop, this is set to the delta-time of the frame.
    /// </summary>
    float dT { get; }
    IDialogueBox? MainDialogue { get; }
    IDialogueBox MainDialogueOrThrow { get; }
    
    /// <summary>
    /// An execution context corresponds to a distinct script execution.
    /// You may nest scripts inside of each other to share code. For example, a flashback within a flashback
    ///  can be coded as three separate code blocks. You can execute them as sequential tasks, or you can wrap
    ///  their execution in Open/CloseExecCtx.
    /// <br/>If execution is wrapped in Open/CloseExecCtx, then the nested script can use "skip read" functionality
    /// even if it has only been seen in a different context.
    /// </summary>
    IVNExecCtx ExecCtx { get; }
    IEnumerable<IVNExecCtx> ExecCtxes { get; }
    
    RenderGroup DefaultRenderGroup { get; }

    public IDisposable AddEntity(IEntity ent);
    public IDisposable AddRenderGroup(RenderGroup rg);
    public void OpenExecCtx(string scriptId);
    public void CloseExecCtx();

    //IVNExecCtx? ForceViewExecCtx { get; set; }
    
    void Update(float deltaTime);

    /// <summary>
    /// Create a lazy task that completes when a Confirm is sent to the VNState (see below).
    /// </summary>
    VNComfirmTask SpinUntilConfirm(VNOperation? preceding = null);
    
    /// <summary>
    /// Use this to proceed operations that require confirmation via SpinUntilConfirm.
    /// </summary>
    void Confirm();
    
    /// <summary>
    /// Use this to skip animations or the like.
    /// </summary>
    void SkipOperation();


    void RecordCG(IGalleryable cg);
    void UpdateSavedata();
    
    /// <summary>
    /// Cascade destroy all currently running enumerators, then destroy all objects.
    /// </summary>
    void DeleteAll();
    
    Event<IEntity> EntityCreated { get; }
    /// <summary>
    /// Called immediately before an interrogator is started (but not if it is skipped).
    /// </summary>
    IInterrogatorSubject InterrogatorCreated { get; }
    /// <summary>
    /// Null if no target is waiting for a confirm.
    /// </summary>
    Evented<IConfirmationReceiver?> AwaitingConfirm { get; }
    
    /// <summary>
    /// When this is set to false, the VN is destroyed and no further operations can be run.
    /// </summary>
    Evented<bool> VNStateActive { get; }
    ISubject<LogMessage> Logs { get; }
}
public class VNState : IVNState, IConfirmationReceiver {
    public float dT { get; private set; }
    private readonly InstanceData? saveData;
    private bool vnUpdated = false;
    private readonly Coroutines cors = new();
    private DMCompactingArray<IEntity> Entities { get; } = new();
    public IDialogueBox? MainDialogue { get; private set; }

    /// <summary>
    /// Cancellation token provided by external controls.
    /// </summary>
    private readonly ICancellee extCToken;
    /// <summary>
    /// Cancellation token governing the lifetime of the VNState.
    /// </summary>
    private readonly Cancellable lifetimeToken;
    /// <summary>
    /// Cancellation token used by the unique confirmation task.
    /// </summary>
    private Cancellable? confirmToken;
    public ICancellee CToken { get; }
    private StackList<VNExecCtx> ExecCtxes { get; } = new();
    
    public RenderGroup DefaultRenderGroup { get; }
    public DMCompactingArray<RenderGroup> RenderGroups { get; } = new();
    
    public Event<IEntity> EntityCreated { get; } = new();
    public ReplayEvent<RenderGroup> RenderGroupCreated { get; } = new(1);
    public IInterrogatorSubject InterrogatorCreated { get; } = new InterrogatorEvent();
    public Evented<IConfirmationReceiver?> AwaitingConfirm { get; } = new(null);
    public ISubject<LogMessage> Logs { get; } = new Event<LogMessage>();
    public Evented<bool> VNStateActive { get; } = new(true);
    
    public IDialogueBox MainDialogueOrThrow =>
        MainDialogue ?? throw new Exception("No dialogue boxes are provisioned.");
    public VNExecCtx ExecCtx => ExecCtxes.Peek();
    
    IEnumerable<IVNExecCtx> IVNState.ExecCtxes => ExecCtxes;
    IVNExecCtx IVNState.ExecCtx => ExecCtx;

    public VNState(ICancellee extCToken, string? scriptId=null, InstanceData? save=null) {
        if (scriptId != null && save == null)
            throw new Exception("Non-null scripts must have a savedata object provided.");
        this.extCToken = extCToken;
        this.saveData = save;
        lifetimeToken = new Cancellable();
        CToken = new JointCancellee(extCToken, lifetimeToken);
        OpenExecCtx(scriptId);

        DefaultRenderGroup = new RenderGroup(this, visible: true);
    }
    
    public void OpenExecCtx(string? scriptId) {
        ExecCtxes.Push(new VNExecCtx(this, ExecCtxes.TryPeek(), scriptId));
        if ((saveData?.Location.TryN(ExecCtxes.Count - 1) ?? null).Try(out var line)) {
            if (scriptId == line.scriptId) {
                ExecCtx.LoadUntil(line.line);
            }
        }
    }

    public void CloseExecCtx() {
        if (ExecCtxes.Count <= 1)
            throw new Exception($"Cannot close ExecCtx when there are {ExecCtxes.Count} remaining");
        ExecCtxes.Pop();
    }

    public void Update(float deltaTime) {
        this.AssertActive();
        dT = deltaTime;
        cors.Step();
        vnUpdated = true;
        for (int ii = 0; ii < RenderGroups.Count; ++ii) {
            if (RenderGroups.ExistsAt(ii))
                RenderGroups[ii].Update(deltaTime);
        }
        RenderGroups.Compact();
        for (int ii = 0; ii < Entities.Count; ++ii) {
            if (Entities.ExistsAt(ii))
                Entities[ii].Update(deltaTime);
        }
        Entities.Compact();
        vnUpdated = false;
    }

    public IDisposable AddEntity(IEntity ent) {
        var dsp = Entities.Add(ent);
        EntityCreated.OnNext(ent);
        if (ent is IDialogueBox dlg)
            MainDialogue ??= dlg;
        return dsp;
    }

    /// <summary>
    /// Convenience method for adding entities to the VNState.
    /// Dispatches to ent.AddToVNState and its specialized forms for IRendered.
    /// </summary>
    public C Add<C>(C ent, RenderGroup? renderGroup = null, int? sortingID = null) where C : IEntity {
        ent.AddToVNState(this);
        if (ent is IRendered r)
            r.AddToRenderGroup(renderGroup ?? DefaultRenderGroup, sortingID);
        return ent;
    }

    public IDisposable AddRenderGroup(RenderGroup rg) {
        if (RenderGroups.Any(x => x.Priority == rg.Priority.Value))
            throw new Exception("Cannot have multiple render groups with the same priority");
        var dsp = RenderGroups.Add(rg);
        RenderGroupCreated.OnNext(rg);
        return dsp;
    }
        
        
    
    public VNOperation Wait(float time) {
        return new VNOperation(this.AssertActive(), ExecCtx, cT => {
            Run(WaitingUtils.WaitFor(time, WaitingUtils.GetCompletionAwaiter(out var t), cT, () => dT));
            return t;
        });
    }

    public void Run(IEnumerator ienum) {
        this.AssertActive();
        cors.Run(ienum,
            //Correct for the one-frame delay created by TryStepPrepend if this is called after VN finishes its cors.step
            new CoroutineOptions(execType: vnUpdated ? CoroutineType.StepTryPrepend : CoroutineType.TryStepPrepend));
    }

    public VNOperation Parallel(params VNOperation[] ops) => VNOperation.Parallel(ops);

    public LazyAwaitable Sequential(params LazyAwaitable[] tasks) => new LazyTask(async () => {
        foreach (var t in tasks)
            await t.Task;
    });

    public VNComfirmTask SpinUntilConfirm(VNOperation? preceding = null) {
        this.AssertActive();
        return new VNComfirmTask(preceding, () => {
            if (ExecCtx.Skipping)
                return Task.FromResult(Completion.SoftSkip);
            if (AwaitingConfirm.Value == null)
                AwaitingConfirm.Value = this;
            confirmToken ??= new Cancellable();
            Run(WaitingUtils.Spin(WaitingUtils.GetCompletionAwaiter(out var t), confirmToken));
            return t;
        });
    }

    public void Confirm() {
        this.AssertActive();
        if (confirmToken != null) {
            AwaitingConfirm.Value = null;
            confirmToken?.Cancel(CancelHelpers.SoftSkipLevel);
            confirmToken = null;
        }
    }
    
    public void SkipOperation() {
        this.AssertActive();
        ExecCtx.SkipOperation();
    }

    public async Task<T> Ask<T>(IInterrogator<T> asker, bool throwIfExists=true) {
        if (saveData != null) {
            if (asker.Key == null)
                throw new Exception("All interrogators operating on a non-null script must have a save key.");
            if (saveData.TryGetData<T>(asker.Key, out var v)) {
                if (ExecCtx.Skipping) {
                    asker.Skip(v);
                    return v;
                } else if (throwIfExists)
                    throw new Exception($"KVR key {asker.Key} already exists.");
            }
        }
        InterrogatorCreated.OnNext(asker);
        var nv = await asker.Start(CToken);
        saveData?.SaveData(asker.Key, nv);
        return nv;
    }

    public void RecordCG(IGalleryable cg) {
        saveData?.GlobalData.GalleryCGViewed(cg.Key);
    }

    public void DeleteAll() {
        lifetimeToken.Cancel(CancelHelpers.HardCancelLevel);
        for (int ii = 0; ii < Entities.Count; ++ii) {
            if (Entities.ExistsAt(ii))
                Entities[ii].Delete();
        }
        Entities.Compact();
        if (Entities.Count > 0)
            throw new Exception("Some VNState entities were not deleted in the cull process. " +
                                $"Script {ExecCtx.ScriptID} has {Entities.Count} remaining.");
        cors.Close();
        if (cors.Count > 0)
            throw new Exception($"Some VNState coroutines were not closed in the cull process. " +
                                $"Script {ExecCtx.ScriptID} has {cors.Count} remaining.");
        VNStateActive.OnNext(false);
    }

    public void UpdateSavedata() {
        if (saveData == null) return;
        saveData.SaveVNLocation(this);
        saveData.GlobalData.LineRead(ExecCtx.ScriptID, ExecCtx.Line);
    }
    
}
}