﻿using System;
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
using BagoumLib.Transitions;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Display;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {

public interface IVNState : IConfirmationReceiver {
    IInstanceData InstanceData { get; }
    IGlobalData GlobalData => InstanceData.GlobalData;
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
    /// True if autoplay and fastforward are allowed as operations.
    /// </summary>
    bool AutoplayFastforwardAllowed { get; }
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
    /// Add an entity to the VNState.
    /// </summary>
    public C Add<C>(C ent, RenderGroup? renderGroup = null, int? sortingID = null) where C : IEntity;
    
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
    /// Run a coroutine.
    /// </summary>
    void Run(IEnumerator ienum);

    /// <summary>
    /// Create a lazy task that completes when a Confirm is sent to the VNState (see below).
    /// </summary>
    VNConfirmTask SpinUntilConfirm(VNOperation? preceding = null);

    /// <summary>
    /// Use this to proceed operations that require confirmation via SpinUntilConfirm.
    /// <returns>True iff a confirmation occurred.</returns>
    /// </summary>
    bool UserConfirm();
    
    /// <summary>
    /// Get a cToken that indicates when a task has been cancelled via skip.
    /// <br/>The caller should dispose the IDisposable when their task is complete, whether or not it has been skipped.
    /// </summary>
    public IDisposable GetOperationCanceller(out VNProcessGroup cT, bool allowUserSkip = true);
    
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

    /// <summary>
    /// Executes the bounded context, saves the output value in instance save data, and returns the output value.
    /// </summary>
    /// <returns></returns>
    Task<T> ExecuteContext<T>(BoundedContext<T> ctx, Func<Task<T>> innerTask);
    

    void RecordCG(IGalleryable cg);

    /// <summary>
    /// Event that is published whenever the instance data changes,
    ///  either due to the ending of a <see cref="BoundedContext{T}"/>
    ///  or a value manually saved via <see cref="VNState.SaveContextValue{T}"/>.
    /// </summary>
    Event<IInstanceData> InstanceDataChanged { get; }
    
    /// <summary>
    /// Update and return the save data.
    /// </summary>
    IInstanceData UpdateInstanceData();
    
    /// <summary>
    /// Cascade destroy all currently running enumerators, then destroy all objects.
    /// </summary>
    void DeleteAll();
    
    /// <summary>
    /// Log of all dialogue passed through this VNState.
    /// </summary>
    AccEvent<DialogueOp> DialogueLog { get; }
    Event<IEntity> EntityCreated { get; }
    IObservable<RenderGroup> RenderGroupCreated { get; }
    /// <summary>
    /// Null if no target is waiting for a confirm.
    /// </summary>
    ICSubject<IConfirmationReceiver?> AwaitingConfirm { get; }
    
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

    bool TryGetContextResult<T>(out T value, params string[] contextKeys);
}
[PublicAPI]
public class VNState : IVNState {
    public float dT { get; private set; }
    /// <summary>
    /// If this value is set (via LoadLocation),
    /// the VNState will almost instantaneously skip forward to the given location.
    /// </summary>
    public (VNLocation location, Action? onLoaded)? LoadTo { get; private set; }

    public void LoadToLocation(VNLocation target, Action? onLoaded = null) {
        LoadTo = (target, onLoaded);
        Logs.OnNext(new LogMessage($"Began loading to location {LoadTo}", LogLevel.INFO));
    }

    private void StopLoading() {
        Logs.OnNext(new LogMessage($"Finished loading to location {LoadTo}", LogLevel.INFO));
        var c = LoadTo?.onLoaded;
        LoadTo = null;
        c?.Invoke();
    }

    /// <summary>
    /// While this value is true, the current operation should be skipped in order to load to the target LoadTo.
    /// </summary>
    private bool IsLoadSkipping => LoadTo is var (location, _) &&
                                    //Skip until the contexts match
                                    (!location.ContextsMatch(Contexts) ||
                                     //Skip until the operation matches
                                     location.LastOperationID != OperationID.Value);

    private SkipMode? userSetSkipMode = null;
    /// <summary>
    /// True iff only read text can be fast-forwarded. 
    /// </summary>
    public bool FastforwardReadTextOnly = true;
    
    /// <summary>
    /// True iff the entire VN sequence can be skipped with a single button.
    /// </summary>
    public bool AllowFullSkip { get; set; } = false;
    
    public bool SetSkipMode(SkipMode? mode) {
        if (mode == SkipMode.LOADING)
            throw new Exception("Cannot set skip mode to LOADING directly. Use LoadToLocation instead.");
        if (userSetSkipMode == mode)
            mode = null;
        if (mode is SkipMode.AUTOPLAY or SkipMode.FASTFORWARD && !AutoplayFastforwardAllowed) {
            Logs.OnNext($"User tried to set skip mode to {mode}, but this is not allowed for this VNState.");
            return false;
        }
        userSetSkipMode = mode;
        if (mode == SkipMode.FASTFORWARD)
            SkipOperation();
        if (SkippingMode != null && CurrentProcesses?.ConfirmToken != null)
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
    /// <summary>
    /// True iff bounded contexts with Unit return type can be skipped
    /// even if they have no save data.
    /// </summary>
    public bool DefaultLoadSkipUnit { get; set; } = false;
    
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
    private Stack<VNInterruptionLayer> interrupts = new();
    private VNInterruptionLayer CurrentInterrupt => interrupts.Peek();
    private VNProcessGroup? CurrentProcesses => CurrentInterrupt.CurrentProcesses;
    public Event<VNInterruptionLayer> InterruptionStarted { get; } = new();
    public Event<VNInterruptionLayer> InterruptionEnded { get; } = new();
    public ICancellee CToken { get; }
    public List<IBoundedContext> Contexts { get; } = new();
    public Event<IBoundedContext> ContextStarted { get; } = new();
    public Event<IBoundedContext> ContextFinished { get; } = new();
    public Event<IInstanceData> InstanceDataChanged { get; } = new();

    //Separate key used for automatically saving BoundedContext results.
    // The fancy prefix is to avoid collsiion with manually-executed saved data.
    public static string ComputeContextsResultKey(IEnumerable<string> ctxIds) => 
        "$$__ctxResult__$$::" + ComputeContextsKey(ctxIds);
    
    //Separate key used for saving contingent values within specific contexts.
    // The fancy prefix is to avoid collsiion with manually-executed saved data.
    public static string ComputeContextsValueKey(string varName, IEnumerable<string> ctxIds) => 
        $"$$__ctxValue[{varName}]__$$::" + ComputeContextsKey(ctxIds);

    public static string ComputeContextsKey(IEnumerable<string> ctxIds) =>
        string.Join("::", ctxIds);

    private IEnumerable<string> ContextIDs => Contexts.Select(c => c.ID);
    public string ContextsKey => ComputeContextsKey(ContextIDs);
    private string ContextsResultKey => ComputeContextsResultKey(ContextIDs);
    private string ContextsValueKey(string key) => ComputeContextsValueKey(key, ContextIDs);
    
    public RenderGroup DefaultRenderGroup { get; }
    public DMCompactingArray<RenderGroup> RenderGroups { get; } = new();
    
    public Event<IEntity> EntityCreated { get; } = new();
    
    //Use a replay event because the first render group will usually be created before any listeners are attached
    private ReplayEvent<RenderGroup> _RenderGroupCreated { get; } = new(1);
    public IObservable<RenderGroup> RenderGroupCreated => _RenderGroupCreated;
    public ICSubject<IConfirmationReceiver?> AwaitingConfirm => _awaitingConfirm;
    private LazyEvented<IConfirmationReceiver?> _awaitingConfirm;
    public Evented<bool> InputAllowed { get; } = new(true);
    private const string OPEN_OPID = "$$__OPEN__$$";
    public Evented<string> OperationID { get; } = new(OPEN_OPID);
    public ISubject<LogMessage> Logs { get; } = new Event<LogMessage>();
    public Evented<bool> VNStateActive { get; } = new(true);
    
    public IInstanceData InstanceData { get; }

    public IDialogueBox MainDialogueOrThrow =>
        MainDialogue ?? throw new Exception("No dialogue boxes are provisioned.");
    public IBoundedContext LowestContext => Contexts[^1];
    

    /// <summary>
    /// 
    /// </summary>
    /// <param name="extCToken">Cancellation token bounding the execution of the VN</param>
    /// <param name="save">Save file for the VN</param>
    public VNState(ICancellee extCToken, IInstanceData save) {
        this.extCToken = extCToken;
        this.InstanceData = save;
        lifetimeToken = new Cancellable();
        CToken = new JointCancellee(extCToken, lifetimeToken);
        interrupts.Push(new(this, null));
        _awaitingConfirm = new(() => CurrentProcesses?.ConfirmReceiver, 
            InterruptionStarted.Erase(), InterruptionEnded.Erase());

        Tokens.Add(OperationID.Subscribe(id => { 
            //TODO: globalData does not contextualize lineRead with context -- is this a problem?
            if (SkippingMode == SkipMode.FASTFORWARD && FastforwardReadTextOnly && !InstanceData.GlobalData.IsLineRead(id))
                SetSkipMode(null);
            InstanceData.GlobalData.LineRead(id);
            if (LoadTo is var (location, _) && location.ContextsMatch(Contexts) && id == location.LastOperationID) {
                StopLoading();
            }
        }));
        Tokens.Add(ContextStarted.Subscribe(ctx => OperationID.OnNext(OPEN_OPID + "::" + ctx.ID)));
        Tokens.Add(ContextFinished.Subscribe(c => {
            if (Contexts.Count == 0 && SkippingMode is SkipMode.FASTFORWARD or SkipMode.AUTOPLAY)
                SetSkipMode(null);
        }));

        // ReSharper disable once VirtualMemberCallInConstructor
        DefaultRenderGroup = MakeDefaultRenderGroup();
    }

    protected virtual RenderGroup MakeDefaultRenderGroup() => new(this, visible: true);

    public IDisposable GetOperationCanceller(out VNProcessGroup op, bool allowUserSkip=true) {
        op = CurrentInterrupt.GetOrMakeProcessGroup();
        op.userSkipAllowed &= allowUserSkip;
        if (SkippingMode.SkipsOperations())
            SkipOperation();
        return op.CreateSubOp();
    }

    /// <summary>
    /// Wrap a task in a BoundedContext so its result is stored in the instance save
    ///  and can be read during the skip-load process (or at any future time during execution).
    /// <br/>This should be executed for all external tasks that are awaited within VN execution, as
    ///  well as for any computed flags that may change throughout execution (eg. global route flags).
    /// <br/>Note that <see cref="Ask{T}"/> automatically wraps its task in a BoundedContext.
    /// </summary>
    public BoundedContext<T> Bound<T>(Func<Task<T>> task, string key, Maybe<T>? deflt = null) =>
        new(this, key, task) { LoadingDefault = deflt ?? Maybe<T>.None };

    public BoundedContext<T> Bound<T>(Task<T> task, string key, Maybe<T>? deflt = null) =>
        Bound(() => task, key, deflt);

    public BoundedContext<T> Bound<T>(T value, string key, Maybe<T>? deflt = null) =>
        Bound(Task.FromResult(value), key, deflt);

    /// <summary>
    /// Alias for <see cref="Bound{T}"/> when computing a boolean flag.
    /// </summary>
    public BoundedContext<bool> ComputeFlag(bool value, string key, bool? deflt = null) =>
        Bound(value, key, deflt.AsMaybe());


    public async Task<T> ExecuteContext<T>(BoundedContext<T> ctx, Func<Task<T>> innerTask) {
        this.AssertActive();
        using var openCtx = new OpenedContext<T>(this, ctx);
        //When load skipping, we can skip the entire context if the current context stack does not match the target
        if (LoadTo?.location.ContextsMatchPrefix(Contexts) == false) {
            if (InstanceData.TryGetData<T>(ContextsResultKey, out var res) || 
                 ctx.LoadingDefault.Try(out res)) {
                Logs.OnNext($"Load-skipping section {ContextsKey} with return value {res}");
                ctx.ShortCircuit?.Invoke();
                return res;
            } else if (typeof(T) == typeof(Unit) && DefaultLoadSkipUnit) {
                Logs.OnNext($"Load-skipping section {ContextsKey} with default Unit return");
                ctx.ShortCircuit?.Invoke();
                return default!;
            } else
                throw new Exception($"Requested to load-skip context {ContextsKey}, but save data does not have " +
                                    "corresponding information");
        }
        var result = await innerTask();
        Logs.OnNext($"Saving result {result} to context {ContextsKey}");
        InstanceData.SaveData(ContextsResultKey, result);
        InstanceDataChanged.OnNext(InstanceData);
        return result;
    }

    public bool TryGetContextResult<T>(out T value, params string[] contextKeys) =>
        InstanceData.TryGetData(ComputeContextsResultKey(contextKeys), out value);
    
    /// <summary>
    /// Get the saved result (ie. return value) of the context with the provided ID list.
    /// <br/>Will throw if the variable is not assigned.
    /// </summary>
    public T GetContextResult<T>(params string[] contextKeys) =>
        InstanceData.GetData<T>(ComputeContextsResultKey(contextKeys));

    public bool TryGetContextValue<T>(string varName, out T value, params string[] contextKeys) =>
        InstanceData.TryGetData(ComputeContextsValueKey(varName, contextKeys), out value);
    
    /// <summary>
    /// Get a saved variable assigned to the context with the provided ID list.
    /// <br/>Will throw if the variable is not assigned.
    /// </summary>
    public T GetContextValue<T>(string varName, params string[] contextIDs) =>
        InstanceData.GetData<T>(ComputeContextsValueKey(varName, contextIDs));
    
    public bool TryGetLocalValue<T>(string varName, out T value) =>
        InstanceData.TryGetData(ContextsValueKey(varName), out value);

    /// <summary>
    /// Get a saved variable assigned to the current context.
    /// <br/>Will throw if the variable is not assigned.
    /// </summary>
    public T GetLocalValue<T>(string varName) =>
        InstanceData.GetData<T>(ContextsValueKey(varName));
    
    /// <summary>
    /// Try to get a saved variable assigned to the current context.
    /// <br/>If it does not exist, create the variable and assign it a
    ///  value from the defaulter function.
    /// </summary>
    public T GetLocalValueOrDefault<T>(string varName, Func<T> defaulter) {
        if (!TryGetLocalValue<T>(varName, out var result))
            SaveLocalValue(varName, result = defaulter());
        return result;
    }

    /// <summary>
    /// Save a variable to the context with the provided ID list. It can be accessed via
    /// <see cref="GetContextValue{T}"/> or <see cref="GetLocalValue{T}"/>.
    /// </summary>
    public void SaveContextValue<T>(string varName, T value, params string[] contextIDs) {
        InstanceData.SaveData(ComputeContextsValueKey(varName, contextIDs), value);
        InstanceDataChanged.OnNext(InstanceData);
    }

    /// <summary>
    /// Save a variable to the current bounded context. It can be accessed via
    /// <see cref="GetContextValue{T}"/> or <see cref="GetLocalValue{T}"/>.
    /// </summary>
    public void SaveLocalValue<T>(string varName, T value) {
        InstanceData.SaveData(ContextsValueKey(varName), value);
        InstanceDataChanged.OnNext(InstanceData);
    }

    /// <summary>
    /// Get the boolean result of a bounded context.
    /// <br/>Alias for <see cref="GetContextValue{T}"/> with T=bool
    /// </summary>
    public bool GetFlag(params string[] contextIDs) => GetContextResult<bool>(contextIDs);

    private class OpenedContext<T> : IDisposable {
        private readonly VNState vn;
        private readonly BoundedContext<T> ctx;
        
        public OpenedContext(VNState vn, BoundedContext<T> ctx) {
            this.vn = vn;
            this.ctx = ctx;
            vn.Contexts.Add(ctx);
            vn.ContextStarted.OnNext(ctx);
        }

        public void Dispose() {
            if (vn.LowestContext != ctx)
                throw new Exception("Contexts closed in wrong order. This is an engine error. Please report this.");
            vn.Contexts.RemoveAt(vn.Contexts.Count - 1);
            vn.ContextFinished.OnNext(ctx);
        }
    }

    /// <summary>
    /// Update with a timestep of zero to flush any cancelled coroutines.
    /// </summary>
    public void Flush() => Update(0);
    
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

    public T Find<T>() {
        for (int ii = 0; ii < Entities.Count; ++ii)
            if (Entities.ExistsAt(ii) && Entities[ii] is T obj)
                return obj;
        throw new Exception($"Couldn't find entity of type {typeof(T)}");
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

    public C Add<C>(C ent, RenderGroup? renderGroup = null, int? sortingID = null) where C : IEntity {
        var dsp = Entities.Add(ent);
        if (ent is IDialogueBox dlg) {
            MainDialogue ??= dlg;
            dlg.DialogueStarted.Subscribe(DialogueLog.OnNext);
        }
        ent.AddToVNState(this, dsp);
        if (ent is IRendered r)
            r.AddToRenderGroup(renderGroup ?? DefaultRenderGroup, sortingID);
        EntityCreated.OnNext(ent);
        return ent;
    }

    public IDisposable _AddRenderGroup(RenderGroup rg) {
        if (RenderGroups.Any(x => x.Priority == rg.Priority.Value))
            throw new Exception("Cannot have multiple render groups with the same priority");
        var dsp = RenderGroups.Add(rg);
        _RenderGroupCreated.OnNext(rg);
        return dsp;
    }



    public VNOperation Wait(float time) {
        return new VNOperation(this.AssertActive(), cT => {
            Run(WaitingUtils.WaitFor(time, WaitingUtils.GetCompletionAwaiter(out var t), cT, () => dT));
            return t;
        });
    }

    public VNOperation Wait(Func<bool> condition) {
        return new VNOperation(this.AssertActive(), cT => {
            Run(WaitingUtils.WaitFor(condition, WaitingUtils.GetCompletionAwaiter(out var t), cT));
            return t;
        });
    }

    public void Run(IEnumerator ienum) {
        this.AssertActive();
        cors.Run(ienum,
            //Correct for the one-frame delay created by TryStepPrepend if this is called after VN finishes its cors.step
            new CoroutineOptions(execType: vnUpdated ? CoroutineType.StepTryPrepend : CoroutineType.TryStepPrepend));
    }

    public ILazyAwaitable Parallel(params ILazyAwaitable[] tasks) => new ParallelLazyAwaitable(tasks);

    public ILazyAwaitable Sequential(params ILazyAwaitable[] tasks) => new SequentialLazyAwaitable(tasks);

    /// <summary>
    /// Add a new interruption layer. This hangs the VNOperations on the current layer
    ///  until it is resumed.
    /// </summary>
    public IVNInterruptionToken Interrupt() => new InterruptionToken(this, CurrentInterrupt);

    private class InterruptionToken : IVNInterruptionToken {
        public VNState VN { get; }
        public VNInterruptionLayer Interruptee => inner.Layer;
        private readonly VNInterruptionLayer.ProcessGroupInterruption inner;
        public InterruptionToken(VNState vn, VNInterruptionLayer layer) {
            this.VN = vn;
            this.inner = layer.Interrupt();
            VN.Flush();
            var newLayer = new VNInterruptionLayer(VN, Interruptee);
            VN.interrupts.Push(newLayer);
            VN.InterruptionStarted.OnNext(newLayer);
        }

        public void ReturnInterrupt(InterruptionStatus resultStatus) {
            if (VN.interrupts.Peek().Parent == Interruptee) {
                VN.InterruptionEnded.OnNext(VN.interrupts.Pop());
                inner.ReturnInterrupt(resultStatus);
            }
        }
    }
    
    public VNConfirmTask SpinUntilConfirm(VNOperation? preceding = null) {
        this.AssertActive();
        return new VNConfirmTask(this, preceding, op => {
            if (SkippingMode == SkipMode.LOADING)
                return Task.FromResult(Completion.SoftSkip);
            var awaiter = WaitingUtils.GetCompletionAwaiter(out var t);
            Run(
                //If an confirm is interrupted, then when it returns to execution,
                // the confirm should be skipped and the next operation should be run.
                WaitingUtils.WaitFor(() => op.WasInterrupted, c => {
                        awaiter(c);
                        op.DoConfirm(); //in case we went through interruption shortcut
                        _awaitingConfirm.Recompute();
                    }, 
                    //Use StrongCancellee since we don't respect soft-skips (from Fastforward or from interruption)
                    new JointCancellee(CToken, JointCancellee.From(new StrongCancellee(op), op.AwaitConfirm()))));
            _awaitingConfirm.Recompute();
            if (SkippingMode.IsPlayerControlled() && SkippingMode != null) 
                Run(AutoconfirmAfterDelay(op, SkippingMode.Value));
            return t;
        });
    }

    private IEnumerator AutoconfirmAfterDelay(VNProcessGroup op, SkipMode s) {
        var cT = op.ConfirmToken;
        var time = s == SkipMode.FASTFORWARD ? TimePerFastforwardConfirm : TimePerAutoplayConfirm;
        for (float elapsed = 0; elapsed < time; elapsed += dT) {
            if (lifetimeToken.Cancelled) break;
            yield return null;
        }
        //Ensure that only the same operation is cancelled
        if (SkippingMode == s && cT == op.ConfirmToken)
            op.DoConfirm();
        _awaitingConfirm.Recompute();
    }
    
    private void _Confirm() {
        if (interrupts.TryPeek(out var op))
            op.DoConfirm();
        _awaitingConfirm.Recompute();
    }

    public bool UserConfirm() {
        this.AssertActive();
        if (interrupts.TryPeek(out var op) && op.ConfirmToken != null) {
            //User confirm cancels autoskip
            if (SkippingMode.IsPlayerControlled()) {
                Logs.OnNext($"Cancelling skip mode {SkippingMode} due to user confirm input.");
                SetSkipMode(null);
            }
            _Confirm();
            return true;
        } else
            return false;
    }

    public void SkipOperation() {
        CurrentProcesses?.operationCTS.Cancel(ICancellee.SoftSkipLevel);
    }
    
    public bool RequestSkipOperation() {
        this.AssertActive();
        //User skip cancels autoskip
        if (SkippingMode.IsPlayerControlled()) {
            Logs.OnNext($"Cancelling skip mode {SkippingMode} due to user skip input.");
            SetSkipMode(null);
        }
        if (CurrentProcesses?.userSkipAllowed is true) {
            SkipOperation();
            return true;
        } else
            return false;
    }

    public void RecordCG(IGalleryable cg) {
        InstanceData.GlobalData.GalleryCGViewed(cg.Key);
    }

    protected virtual void _DeleteAll() {
        Logs.OnNext($"Deleting all entities within VNState {this}");
        lifetimeToken.Cancel(ICancellee.HardCancelLevel);
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

    public IInstanceData UpdateInstanceData() {
        InstanceData.Location = VNLocation.Make(this);
        return InstanceData;
    }


    public virtual void OpenLog() {
        throw new NotImplementedException();
    }

    public virtual void PauseGameplay() {
        throw new NotImplementedException();
    }
}
}