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
    /// A list of script contexts. A bounded context is defined as a unit of execution which
    /// can be skipped in its entirety during loading if it has already been fully executed.
    /// This requires that it leaves no hanging objects after it is complete.
    /// </summary>
    List<IBoundedContext> Contexts { get; }
    
    /// <summary>
    /// The most recently opened context.
    /// </summary>
    IBoundedContext LowestContext { get; }

    /// <summary>
    /// Returns the type of skipping the VN currently has active.
    /// </summary>
    public SkipMode? SkippingMode { get; }

    /// <summary>
    /// Set the skip mode. Note that you cannot set the skip mode to LOADING.
    /// <br/>Some modes may be disabled (eg. autoplay, fastforward may be disabled for replay-safe uses).
    /// If you try to set the skip mode to a disabled mode, the function will return false. Otherwise,
    /// it will return true.
    /// </summary>
    public bool SetSkipMode(SkipMode? mode);
    
    /// <summary>
    /// If the VNState allows full-skipping, then skips the entire VNState (ie. destroy it).
    /// <br/>Returns true iff a skip was performed.
    /// </summary>
    /// <returns></returns>
    bool TryFullSkip();
    
    /// <summary>
    /// The amount of time the VNState will wait before executing confirm commands while autoplaying.
    /// </summary>
    public float TimePerAutoplayConfirm { get; set; }
    /// <summary>
    /// The amount of time the VNState will wait before executing confirm commands while fastforwarding.
    /// </summary>
    public float TimePerFastforwardConfirm { get; set; }

    /// <summary>
    /// Implement this in derived classes to provide a way for entities such as dialogue boxes to pause the game.
    /// </summary>
    public void PauseGameplay();

    /// <summary>
    /// Implement this in derived classes to provide a way for entities such as dialogue boxes to open the dialogue log.
    /// </summary>
    public void OpenLog();

    RenderGroup DefaultRenderGroup { get; }
    
    /// <summary>
    /// This should generally be called by the entity constructor. Script code should use vn.Add or whatever function is provided.
    /// </summary>
    IDisposable AddEntity(IEntity ent);
    /// <summary>
    /// This is called within the RenderGroup constructor. You do not need to call it explicitly.
    /// </summary>
    IDisposable _AddRenderGroup(RenderGroup rg);
    
    /// <summary>
    /// Update all entities controlled by the VNState.
    /// </summary>
    /// <param name="deltaTime"></param>
    void Update(float deltaTime);

    /// <summary>
    /// Create a lazy task that completes when a Confirm is sent to the VNState (see below).
    /// </summary>
    VNConfirmTask SpinUntilConfirm(VNOperation? preceding = null);

    /// <summary>
    /// Use this to proceed operations that require confirmation via SpinUntilConfirm.
    /// </summary>
    void UserConfirm();
    
    /// <summary>
    /// Get a cToken that indicates when a task has been cancelled via skip.
    /// <br/>The caller should dispose the IDisposable when their task is complete, whether or not it has been skipped.
    /// </summary>
    public IDisposable GetOperationCanceller(out ICancellee cT, bool allowUserSkip = true);
    
    /// <summary>
    /// Skip the current operation. This will result in a skip even if user input skips are ignored.
    /// </summary>
    void SkipOperation();
    /// <summary>
    /// Called through user input. Use to skip animations or the like.
    /// <br/>Note that a skip may not occur as a result if user input is set to be ignored.
    /// <br/>Returns true if a skip occurred.
    /// </summary>
    bool RequestSkipOperation();
    

    void RecordCG(IGalleryable cg);
    /// <summary>
    /// Update and return the save data.
    /// </summary>
    /// <returns></returns>
    IInstanceData UpdateSavedata();
    
    /// <summary>
    /// Cascade destroy all currently running enumerators, then destroy all objects.
    /// </summary>
    void DeleteAll();
    
    /// <summary>
    /// Log of all dialogue passed through this VNState.
    /// </summary>
    AccEvent<DialogueOp> DialogueLog { get; }
    Event<IEntity> EntityCreated { get; }
    ReplayEvent<RenderGroup> RenderGroupCreated { get; }
    /// <summary>
    /// Called immediately before an interrogator is started (but not if it is skipped).
    /// </summary>
    IInterrogatorSubject InterrogatorCreated { get; }
    /// <summary>
    /// Null if no target is waiting for a confirm.
    /// </summary>
    Evented<IConfirmationReceiver?> AwaitingConfirm { get; }
    
    /// <summary>
    /// Whether or not VN components should allow user input. Set this to false eg. during pauses.
    /// </summary>
    Evented<bool> InputAllowed { get; }
    
    /// <summary>
    /// When this is set to false, the VN is destroyed and no further operations can be run.
    /// </summary>
    Evented<bool> VNStateActive { get; }
    
    Evented<string> OperationID { get; }
    ISubject<LogMessage> Logs { get; }
}
public class VNState : IVNState, IConfirmationReceiver {
    public float dT { get; private set; }
    
    /// <summary>
    /// If this value is set (via LoadLocation),
    /// the VNState will almost instantaneously skip forward to the given location.
    /// </summary>
    public VNLocation? LoadTo { get; private set; }

    public void LoadToLocation(VNLocation target) {
        LoadTo = target;
        Logs.OnNext(new LogMessage($"Began loading to location {LoadTo}", LogLevel.INFO));
    }

    private void StopLoading() {
        Logs.OnNext(new LogMessage($"Finished loading to location {LoadTo}", LogLevel.INFO));
        LoadTo = null;
    }

    /// <summary>
    /// While this value is true, the current operation should be skipped in order to load to the target LoadTo.
    /// </summary>
    private bool IsLoadSkipping => !(LoadTo is null) &&
                                    //Skip until the contexts match
                                    (!LoadTo.ContextsMatch(Contexts) ||
                                     //Skip until the operation matches
                                     LoadTo.LastOperationID != OperationID.Value);

    private SkipMode? userSetSkipMode = null;
    public bool AllowFullSkip { get; set; } = false;
    
    public bool SetSkipMode(SkipMode? mode) {
        if (mode == SkipMode.LOADING)
            throw new Exception("Cannot set skip mode to LOADING directly. Use LoadToLocation instead.");
        if (userSetSkipMode == mode)
            mode = null;
        if ((mode == SkipMode.AUTOPLAY || mode == SkipMode.FASTFORWARD) && !AutoplayFastforwardAllowed) {
            Logs.OnNext($"User tried to set skip mode to {mode}, but this is not allowed for this VNState.");
            return false;
        }
        userSetSkipMode = mode;
        if (SkippingMode != null && confirmToken != null)
            _Confirm();
        Logs.OnNext(new LogMessage($"Set the user skip mode to {mode}", LogLevel.INFO));
        return true;
    }
    public SkipMode? SkippingMode =>
        IsLoadSkipping ?
            SkipMode.LOADING :
            userSetSkipMode;
    
    public bool TryFullSkip() {
        if (AllowFullSkip) {
            DeleteAll();
            return true;
        }
        return false;
    }

    public bool AutoplayFastforwardAllowed { get; set; } = true;
    public float TimePerAutoplayConfirm { get; set; } = 1f;
    public float TimePerFastforwardConfirm { get; set; } = 0.2f;
    
    private readonly IInstanceData? saveData;
    private bool vnUpdated = false;
    private readonly Coroutines cors = new();
    private DMCompactingArray<IEntity> Entities { get; } = new();
    public IDialogueBox? MainDialogue { get; private set; }
    public AccEvent<DialogueOp> DialogueLog { get; } = new();
    protected List<IDisposable> Tokens { get; } = new();

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
    public List<IBoundedContext> Contexts { get; } = new();
    public Event<IBoundedContext> ContextStarted { get; } = new();
    public Event<IBoundedContext> ContextFinished { get; } = new();
    public string ContextsKey =>
        Contexts.Count == 1 ? Contexts[0].ID :
        string.Join("::", Contexts.Select(c => c.ID));

    public string OperationKey => ContextsKey + $"||{OperationID.Value}";
    public RenderGroup DefaultRenderGroup { get; }
    public DMCompactingArray<RenderGroup> RenderGroups { get; } = new();
    
    public Event<IEntity> EntityCreated { get; } = new();
    public ReplayEvent<RenderGroup> RenderGroupCreated { get; } = new(1);
    public IInterrogatorSubject InterrogatorCreated { get; } = new InterrogatorEvent();
    public Evented<IConfirmationReceiver?> AwaitingConfirm { get; } = new(null);
    public Evented<bool> InputAllowed { get; } = new(true);
    private const string OPEN_OPID = "$$__OPEN__$$";
    public Evented<string> OperationID { get; } = new(OPEN_OPID);
    public ISubject<LogMessage> Logs { get; } = new Event<LogMessage>();
    public Evented<bool> VNStateActive { get; } = new(true);
    
    public IDialogueBox MainDialogueOrThrow =>
        MainDialogue ?? throw new Exception("No dialogue boxes are provisioned.");
    public IBoundedContext LowestContext => Contexts[Contexts.Count - 1];

    public VNState(ICancellee extCToken, IInstanceData? save=null) {
        this.extCToken = extCToken;
        this.saveData = save;
        lifetimeToken = new Cancellable();
        CToken = new JointCancellee(extCToken, lifetimeToken);

        Tokens.Add(OperationID.Subscribe(id => { 
            //TODO: globalData does not contextualize lineRead with context -- is this a problem?
            saveData?.GlobalData.LineRead(id);
            if (!(LoadTo is null) && LoadTo.ContextsMatch(Contexts) && id == LoadTo.LastOperationID) {
                StopLoading();
            }
        }));
        Tokens.Add(ContextStarted.Subscribe(ctx => OperationID.OnNext(OPEN_OPID + "::" + ctx.ID)));

        // ReSharper disable once VirtualMemberCallInConstructor
        DefaultRenderGroup = MakeDefaultRenderGroup();
        
        if (!(save?.Location is null))
            LoadToLocation(save.Location);
    }

    protected virtual RenderGroup MakeDefaultRenderGroup() => new RenderGroup(this, visible: true);

    private OperationCancellation? op = null;
    public int OperationCTokenDependencies { get; private set; } = 0;
    private class OperationCancellation {
        public readonly Cancellable operationCTS = new();
        public bool userSkipAllowed;
        public readonly ICancellee? operationCToken;
        
        public OperationCancellation(VNState vn, bool allowUserSkip) {
            userSkipAllowed = allowUserSkip;
            operationCToken = new JointCancellee(vn.CToken, operationCTS);
        }
    }
    private class SubOpTracker : IDisposable {
        private readonly VNState vn;
        public SubOpTracker(VNState vn) {
            this.vn = vn;
            ++vn.OperationCTokenDependencies;
        }

        public void Dispose() {
            --vn.OperationCTokenDependencies;
        }
    }
    
    public IDisposable GetOperationCanceller(out ICancellee cT, bool allowUserSkip=true) {
        if (op == null || OperationCTokenDependencies <= 0) {
            op = new OperationCancellation(this, allowUserSkip);
            if (SkippingMode.SkipsOperations())
                SkipOperation();
        } else
            op.userSkipAllowed &= allowUserSkip;
        cT = op.operationCToken!;
        return new SubOpTracker(this);
    }
    
    /// <summary>
    /// Executes the context, saves the output value in instance save data, and returns the output value.
    /// </summary>
    /// <returns></returns>
    public ILazyAwaitable<T> ExecuteContext<T>(BoundedContext<T> ctx) => new LazyTask<T>(async () => {
        this.AssertActive();
        using var openCtx = new OpenedContext<T>(this, ctx);
        //When load skipping, we can skip the entire context if the current context stack does not match the target
        if (LoadTo?.ContextsMatchPrefix(Contexts) == false) {
            if (saveData != null && saveData.TryGetData<T>(ContextsKey, out var res)) {
                Logs.OnNext($"Load-skipping section {ContextsKey} with return value {res}");
                ctx.ShortCircuit?.Invoke();
                return res;
            }
            else if (typeof(T) == typeof(Unit)) {
                Logs.OnNext($"Load-skipping section {ContextsKey} with default Unit return");
                ctx.ShortCircuit?.Invoke();
                return default!;
            } else
                throw new Exception($"Requested to load-skip context {ContextsKey}, but save data does not have " +
                                    $"corresponding information and the type {typeof(T)} is not Unit");
        }
        var result = await ctx._InnerTask;
        openCtx.SaveResult(result);
        return result;
    });

    private class OpenedContext<T> : IDisposable {
        private readonly VNState vn;
        private readonly BoundedContext<T> ctx;
        
        public OpenedContext(VNState vn, BoundedContext<T> ctx) {
            this.vn = vn;
            this.ctx = ctx;
            vn.Contexts.Add(ctx);
            vn.ContextStarted.OnNext(ctx);
        }

        /// <summary>
        /// Saves the value for the current context list in the instance save.
        /// <br/>Overwrites by default.
        /// <br/>Returns true iff the value already exists.
        /// </summary>
        /// <returns>null if there is no save data, true if the value already exists in save data, false otherwise.</returns>
        public bool? SaveResult(T value, bool overwrite = true) {
            if (vn.saveData == null)
                return null;
            var key = vn.ContextsKey;
            bool exists = vn.saveData.HasData(key);
            if (!exists || overwrite)
                vn.saveData.SaveData(key, value);
            return exists;
        }
        
        public void Dispose() {
            if (vn.LowestContext != ctx)
                throw new Exception("Contexts closed in wrong order. This is an engine error. Please report this.");
            vn.Contexts.RemoveAt(vn.Contexts.Count - 1);
            vn.ContextFinished.OnNext(ctx);
        }
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
        if (ent is IDialogueBox dlg) {
            MainDialogue ??= dlg;
            dlg.DialogueStarted.Subscribe(DialogueLog.OnNext);
        }
        return dsp;
    }

    public T? FindEntity<T>() where T : class {
        for (int ii = 0; ii < Entities.Count; ++ii)
            if (Entities.ExistsAt(ii) && Entities[ii] is T obj)
                return obj;
        return null;
    }
    public object? FindEntity(Type t) {
        for (int ii = 0; ii < Entities.Count; ++ii)
            if (Entities.ExistsAt(ii) && Entities[ii].GetType().IsWeakSubclassOf(t))
                return Entities[ii];
        return null;
    }
    public List<T> FindEntities<T>() where T : class {
        var results = new List<T>();
        for (int ii = 0; ii < Entities.Count; ++ii)
            if (Entities.ExistsAt(ii) && Entities[ii] is T obj)
                results.Add(obj);
        return results;
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

    public IDisposable _AddRenderGroup(RenderGroup rg) {
        if (RenderGroups.Any(x => x.Priority == rg.Priority.Value))
            throw new Exception("Cannot have multiple render groups with the same priority");
        var dsp = RenderGroups.Add(rg);
        RenderGroupCreated.OnNext(rg);
        return dsp;
    }
        
        
    
    public VNOperation Wait(float time) {
        return new VNOperation(this.AssertActive(), cT => {
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

    public ILazyAwaitable Parallel(params ILazyAwaitable[] ops) =>
        new LazyTask(() => Task.WhenAll(ops.Select(la => la.Task)));

    public ILazyAwaitable Sequential(params ILazyAwaitable[] tasks) => new LazyTask(async () => {
        for (int ii = 0; ii < tasks.Length; ++ii)
            await tasks[ii];
    });

    public VNConfirmTask SpinUntilConfirm(VNOperation? preceding = null) {
        this.AssertActive();
        return new VNConfirmTask(preceding, () => {
            if (SkippingMode == SkipMode.LOADING)
                return Task.FromResult(Completion.SoftSkip);
            if (AwaitingConfirm.Value == null)
                AwaitingConfirm.Value = this;
            confirmToken ??= new Cancellable();
            Run(WaitingUtils.Spin(WaitingUtils.GetCompletionAwaiter(out var t), new JointCancellee(CToken, confirmToken)));
            if (SkippingMode.IsPlayerControlled() && SkippingMode != null)
                Run(AutoconfirmAfterDelay(confirmToken, SkippingMode.Value));
            return t;
        });
    }

    private IEnumerator AutoconfirmAfterDelay(Cancellable confToken, SkipMode s) {
        var time = s == SkipMode.FASTFORWARD ? TimePerFastforwardConfirm : TimePerAutoplayConfirm;
        for (float elapsed = 0; elapsed < time; elapsed += dT)
            yield return null;
        //Ensure that only the same operation is cancelled
        if (SkippingMode == s && confToken == confirmToken)
            _Confirm();
    }
    
    private void _Confirm() {
        AwaitingConfirm.Value = null;
        confirmToken?.Cancel(CancelHelpers.SoftSkipLevel);
        confirmToken = null;
    }

    public void UserConfirm() {
        this.AssertActive();
        if (confirmToken != null) {
            //User confirm cancels autoskip
            if (SkippingMode.IsPlayerControlled()) {
                Logs.OnNext($"Cancelling skip mode {SkippingMode} due to user confirm input.");
                SetSkipMode(null);
            }
            _Confirm();
        }
    }

    public void SkipOperation() {
        op?.operationCTS.Cancel(CancelHelpers.SoftSkipLevel);
    }
    
    public bool RequestSkipOperation() {
        this.AssertActive();
        //User skip cancels autoskip
        if (SkippingMode.IsPlayerControlled()) {
            Logs.OnNext($"Cancelling skip mode {SkippingMode} due to user skip input.");
            SetSkipMode(null);
        }
        if (op?.userSkipAllowed == true) {
            SkipOperation();
            return true;
        } else
            return false;
    }

    public async Task<T> Ask<T>(IInterrogator<T> asker, bool throwIfExists=true) {
        if (saveData != null) {
            if (asker.Key == null)
                throw new Exception("All interrogators operating on a non-null script must have a save key.");
            if (saveData.TryGetData<T>(asker.Key, out var v)) {
                if (SkippingMode.SkipsOperations()) {
                    asker.Skip(v);
                    return v;
                } else if (throwIfExists)
                    throw new Exception($"KVR key {asker.Key} already exists.");
            } else if (SkippingMode == SkipMode.LOADING)
                throw new Exception("Cannot load to location when the save file does not have " +
                                    $"information for interrogation key {asker.Key}");
        } else if (SkippingMode == SkipMode.LOADING)
            throw new Exception("Cannot load to location when there is no save file to provide interrogations.");
        InterrogatorCreated.OnNext(asker);
        var nv = await asker.Start(CToken);
        saveData?.SaveData(asker.Key, nv);
        return nv;
    }

    public void RecordCG(IGalleryable cg) {
        saveData?.GlobalData.GalleryCGViewed(cg.Key);
    }

    protected virtual void _DeleteAll() {
        Logs.OnNext($"Deleting all entities within VNState {this}");
        lifetimeToken.Cancel(CancelHelpers.HardCancelLevel);
        foreach (var t in Tokens)
            t.Dispose();
        for (int ii = 0; ii < RenderGroups.Count; ++ii) {
            if (RenderGroups.ExistsAt(ii))
                RenderGroups[ii].Delete();
        }
        RenderGroups.Compact();
        if (RenderGroups.Count > 0)
            throw new Exception("Some VNState render groups were not deleted in the cull process. " +
                                $"Script {LowestContext.ID} has {RenderGroups.Count} remaining.");
        for (int ii = 0; ii < Entities.Count; ++ii) {
            if (Entities.ExistsAt(ii))
                Entities[ii].Delete();
        }
        Entities.Compact();
        if (Entities.Count > 0)
            throw new Exception("Some VNState entities were not deleted in the cull process. " +
                                $"Script {LowestContext.ID} has {Entities.Count} remaining.");
        cors.Close();
        if (cors.Count > 0)
            throw new Exception($"Some VNState coroutines were not closed in the cull process. " +
                                $"Script {LowestContext.ID} has {cors.Count} remaining.");
        VNStateActive.OnNext(false);
    }
    
    //This is separate from EntityVNState, which is not set until DeleteAll ends
    private bool deleteStarted = false;
    public void DeleteAll() {
        if (deleteStarted || VNStateActive == false) return;
        deleteStarted = true;
        _DeleteAll();
    }

    public IInstanceData UpdateSavedata() {
        if (saveData == null) throw new Exception("There is no save data to update");
        saveData.Location = VNLocation.Make(this);
        return saveData;
    }


    public virtual void OpenLog() {
        throw new NotImplementedException();
    }

    public virtual void PauseGameplay() {
        throw new NotImplementedException();
    }
}
}