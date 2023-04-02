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
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using BagoumLib.Transitions;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Display;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {

/// <summary>
/// A top-level stateful object containing all information for the execution of a visual novel.
/// </summary>
public interface IVNState : IConfirmationReceiver {
    /// <summary>
    /// Save data specific to the save file backing this execution.
    /// </summary>
    IInstanceData InstanceData { get; }
    /// <summary>
    /// Save data common to all save files.
    /// </summary>
    IGlobalData GlobalData => InstanceData.GlobalData;
    /// <summary>
    /// A cancellation token bounded by the lifetime of the <see cref="IVNState"/>.
    /// </summary>
    ICancellee CToken { get; }
    /// <summary>
    /// Within the update loop, this is set to the delta-time of the frame.
    /// </summary>
    float dT { get; }

    /// <summary>
    /// The main dialogue box, if it exists.
    /// </summary>
    IDialogueBox? MainDialogue { get; }
    
    /// <summary>
    /// Get the main dialogue box or throw an exception.
    /// </summary>
    IDialogueBox MainDialogueOrThrow { get; }

    /// <summary>
    /// Before loading, the <see cref="VNState"/> should be initialized with the
    ///  save data that it had *before* running the BCtx being loaded into.
    /// <br/>The instance data at the time of save, with the BCtx partially executed,
    ///  should be passed as `replayer` here.
    /// </summary>
    public void LoadToLocation(VNLocation target, IInstanceData replayer, Action? onLoaded = null);

    /// <summary>
    /// Reset the interruption status on the current process layer.
    /// <br/>Call this after a BCTX is finished running in an ADV context to avoid cross-pollution of interruption.
    /// </summary>
    void ResetInterruptStatus();

    /// <summary>
    /// A list of currently-executing nested <see cref="BoundedContext{T}"/>s.
    /// </summary>
    List<OpenedContext> Contexts { get; }
    
    /// <summary>
    /// An event called when a <see cref="BoundedContext{T}"/> begins, before it is added to <see cref="Contexts"/>.
    /// </summary>
    Event<OpenedContext> ContextStarted { get; }
    
    /// <summary>
    /// An event called when a <see cref="BoundedContext{T}"/> ends, after its data is saved and
    ///  after it is removed from <see cref="Contexts"/>.
    /// </summary>
    Event<OpenedContext> ContextFinished { get; }

    /// <summary>
    /// True iff any <see cref="BoundedContext{T}"/>s are currently executing on the VN.
    /// </summary>
    bool ContextExecuting => Contexts.Count > 0;

    /// <summary>
    /// The most recently opened bounded context (<see cref="Contexts"/>[^1]).
    /// <br/>Throws an exception if <see cref="ContextExecuting"/> is false.
    /// </summary>
    OpenedContext LowestContext => Contexts[^1];

    /// <summary>
    /// Returns the type of skipping the VN currently has active.
    /// </summary>
    SkipMode? SkippingMode { get; }

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

    /// <summary>
    /// The default rendering group.
    /// </summary>
    RenderGroup DefaultRenderGroup { get; }

    /// <summary>
    /// Add an entity to the VNState.
    /// </summary>
    public C Add<C>(C ent, RenderGroup? renderGroup = null, int? sortingID = null) where C : IEntity;

    /// <summary>
    /// Update with a timestep of zero to flush any cancelled coroutines.
    /// </summary>
    public void Flush();
    
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
    /// Add a new interruption layer. This hangs the VNOperations on the current layer
    ///  until it is resumed.
    /// </summary>
    public IVNInterruptionToken Interrupt();

    /// <summary>
    /// Wrap an external task (that does not respect skip/cancel semantics) in a cancellable VNOperation.
    /// <br/>Note that this cannot be skipped. It will loop. (It can be cancelled.)
    /// </summary>
    Task<T> WrapExternal<T>(Task<T> task);
    
    /// <summary>
    /// Wrap an external task (that does not respect skip/cancel semantics) in a <see cref="StrongBoundedContext{T}"/>.
    /// <br/>Note that this cannot be skipped. It will loop. (It can be cancelled.)
    /// </summary>
    StrongBoundedContext<T> WrapExternal<T>(string key, Func<Task<T>> task) =>
        new(this, key, () => WrapExternal(task()));

    /// <summary>
    /// Create a lazy task that completes when a Confirm is sent to the VNState (see <see cref="UserConfirm"/>).
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
    
    /// <summary>
    /// Record a gallery object as having been viewed ingame. (WIP)
    /// </summary>
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
    /// Run Predelete on all objects, then cascade destroy all currently running enumerators, then destroy all objects.
    /// </summary>
    void DeleteAll();
    
    /// <summary>
    /// Log of all dialogue passed through this VNState.
    /// </summary>
    AccEvent<DialogueOp> DialogueLog { get; }
    /// <summary>
    /// Event called when an entity is added to the VNState.
    /// </summary>
    Event<IEntity> EntityCreated { get; }
    /// <summary>
    /// Event called when a render group is added to the VNState.
    /// </summary>
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
    
    /// <summary>
    /// The current operation being executed on the VNState.
    /// </summary>
    Evented<string> OperationID { get; }
    /// <summary>
    /// All logs from executed VN code.
    /// </summary>
    Logger Logs { get; }

    /// <summary>
    /// Try to get the local data for the <see cref="BoundedContext{T}"/> with the given parentage path.
    /// <br/>If a context is executed multiple times in different parent contexts, the local data is not shared.
    /// </summary>
    bool TryGetContextData<T>(out BoundedContextData<T> value, params string[] contextKeys);
    
    /// <summary>
    /// True if it is possible to run a context twice.
    /// <br/>This will avoid throwing a "duplicate definition" exception.
    /// </summary>
    bool AllowsRepeatContextExecution { get; }
}

/// <inheritdoc cref="IVNState"/>
[PublicAPI]
public class VNState : IVNState {
    /// <inheritdoc/>
    public float dT { get; private set; }
    /// <summary>
    /// If this value is set (via LoadLocation),
    /// the VNState will almost instantaneously skip forward to the given location.
    /// </summary>
    public (VNLocation location, IInstanceData replayer, Action? onLoaded)? LoadTo { get; private set; }

    /// <inheritdoc/>
    public void LoadToLocation(VNLocation target, IInstanceData replayer, Action? onLoaded = null) {
        LoadTo = (target, replayer, onLoaded);
        Logs.Log($"Began loading to location {LoadTo}", LogLevel.INFO);
    }

    private void StopLoading() {
        if (!LoadTo.Try(out var lt))
            throw new Exception("Cannot stop loading when load configuration is null");
        Logs.Log($"Finished loading to location {LoadTo}", LogLevel.INFO);
        LoadTo = null;
        InstanceData = lt.replayer;
        if (Contexts.Count > 0)
            Contexts[^1].RemapData(InstanceData);
        lt.onLoaded?.Invoke();
        InstanceDataChanged.OnNext(InstanceData);
    }

    /// <summary>
    /// While this value is true, the current operation should be skipped in order to load to the target LoadTo.
    /// </summary>
    private bool IsLoadSkipping => LoadTo is var (location, _, _) &&
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
    
    /// <inheritdoc/>
    public bool SetSkipMode(SkipMode? mode) {
        if (mode == SkipMode.LOADING)
            throw new Exception("Cannot set skip mode to LOADING directly. Use LoadToLocation instead.");
        if (userSetSkipMode == mode)
            mode = null;
        if (mode is SkipMode.AUTOPLAY or SkipMode.FASTFORWARD && !AutoplayFastforwardAllowed) {
            Logs.Log($"User tried to set skip mode to {mode}, but this is not allowed for this VNState.");
            return false;
        }
        userSetSkipMode = mode;
        if (mode == SkipMode.FASTFORWARD)
            SkipOperation();
        if (SkippingMode != null && CurrentProcesses?.ConfirmToken != null)
            _Confirm();
        Logs.Log($"Set the user skip mode to {mode}", LogLevel.INFO);
        return true;
    }
    /// <inheritdoc/>
    public SkipMode? SkippingMode =>
        IsLoadSkipping ?
            SkipMode.LOADING :
            userSetSkipMode;
    
    /// <inheritdoc/>
    public bool TryFullSkip() {
        if (AllowFullSkip) {
            DeleteAll();
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public bool AutoplayFastforwardAllowed { get; set; } = true;
    /// <inheritdoc/>
    public float TimePerAutoplayConfirm { get; set; } = 1f;
    /// <inheritdoc/>
    public float TimePerFastforwardConfirm { get; set; } = 0.2f;
    /// <summary>
    /// True iff bounded contexts with Unit return type can be skipped
    /// even if they have no save data.
    /// </summary>
    public bool DefaultLoadSkipUnit { get; set; } = false;
    
    private bool vnUpdated = false;
    private readonly Coroutines cors = new();
    private DMCompactingArray<IEntity> Entities { get; } = new();
    /// <inheritdoc/>
    public IDialogueBox? MainDialogue { get; private set; }
    /// <inheritdoc/>
    public AccEvent<DialogueOp> DialogueLog { get; } = new();
    /// <summary>
    /// The disposable tokens within the scope of this object.
    /// </summary>
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

    /// <inheritdoc/>
    public void ResetInterruptStatus() {
        CurrentInterrupt.Status = InterruptionStatus.Normal;
    }
    private VNProcessGroup? CurrentProcesses => CurrentInterrupt.CurrentProcesses;
    /// <summary>
    /// Event called when an interruption layer is added.
    /// </summary>
    public Event<VNInterruptionLayer> InterruptionStarted { get; } = new();
    /// <summary>
    /// Event called when an interruption layer is completed.
    /// </summary>
    public Event<VNInterruptionLayer> InterruptionEnded { get; } = new();
    /// <inheritdoc/>
    public ICancellee CToken { get; }
    /// <inheritdoc/>
    public List<OpenedContext> Contexts { get; } = new();
    /// <inheritdoc/>
    public Event<OpenedContext> ContextStarted { get; } = new();
    /// <inheritdoc/>
    public Event<OpenedContext> ContextFinished { get; } = new();
    /// <inheritdoc/>
    public Event<IInstanceData> InstanceDataChanged { get; } = new();

    /// <summary>
    /// A string describing all the currently-opened <see cref="BoundedContext{T}"/>s. This should only be used
    ///  for logging purposes.
    /// </summary>
    public string ContextsDescriptor => string.Join("::", Contexts.Select(c => c.BCtx.ID));

    /// <inheritdoc/>
    public RenderGroup DefaultRenderGroup { get; }
    /// <summary>
    /// All active rendering groups.
    /// </summary>
    public DMCompactingArray<RenderGroup> RenderGroups { get; } = new();
    
    /// <inheritdoc/>
    public Event<IEntity> EntityCreated { get; } = new();
    
    //Use a replay event because the first render group will usually be created before any listeners are attached
    private ReplayEvent<RenderGroup> _RenderGroupCreated { get; } = new(1);
    /// <inheritdoc/>
    public IObservable<RenderGroup> RenderGroupCreated => _RenderGroupCreated;
    /// <inheritdoc/>
    public ICSubject<IConfirmationReceiver?> AwaitingConfirm => _awaitingConfirm;
    private LazyEvented<IConfirmationReceiver?> _awaitingConfirm;
    /// <inheritdoc/>
    public Evented<bool> InputAllowed { get; } = new(true);
    private const string OPEN_OPID = "$$__OPEN__$$";
    /// <inheritdoc/>
    public Evented<string> OperationID { get; } = new(OPEN_OPID);
    /// <inheritdoc/>
    public Logger Logs { get; } = new();
    /// <inheritdoc/>
    public Evented<bool> VNStateActive { get; } = new(true);
    
    /// <inheritdoc/>
    public IInstanceData InstanceData { get; private set; }

    /// <inheritdoc/>
    public IDialogueBox MainDialogueOrThrow =>
        MainDialogue ?? throw new Exception("No dialogue boxes are provisioned.");

    /// <inheritdoc/>
    public bool AllowsRepeatContextExecution => true;

    /// <summary>
    /// Create a <see cref="VNState"/>.
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
            if (LoadTo is var (location, _, _) && location.ContextsMatch(Contexts) && id == location.LastOperationID) {
                StopLoading();
            }
        }));
        Tokens.Add(ContextFinished.Subscribe(c => {
            if (Contexts.Count == 0 && SkippingMode is SkipMode.FASTFORWARD or SkipMode.AUTOPLAY)
                SetSkipMode(null);
        }));

        // ReSharper disable once VirtualMemberCallInConstructor
        DefaultRenderGroup = MakeDefaultRenderGroup();
    }

    /// <summary>
    /// Create a default rendering group. This is called at the end of the constructor.
    /// </summary>
    protected virtual RenderGroup MakeDefaultRenderGroup() => Add(new RenderGroup(visible: true));

    /// <inheritdoc/>
    public IDisposable GetOperationCanceller(out VNProcessGroup op, bool allowUserSkip=true) {
        op = CurrentInterrupt.GetOrMakeProcessGroup();
        op.userSkipAllowed &= allowUserSkip;
        if (SkippingMode.SkipsOperations())
            SkipOperation();
        return op.CreateOpTracker();
    }
    
    /// <summary>
    /// Execute a <see cref="BoundedContext{T}"/>, nesting its execution within any currently-running contexts.
    /// </summary>
    /// <param name="ctx"><see cref="BoundedContext{T}"/> providing metadata of execution</param>
    /// <param name="innerTask">Task code to execute</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<T> ExecuteContext<T>(BoundedContext<T> ctx, Func<Task<T>> innerTask) {
        this.AssertActive();
        using var openCtx = new OpenedContext<T>(ctx);
        OperationID.OnNext(OPEN_OPID + "::" + ctx.ID);
        //When load skipping, we can skip the entire context if the current context stack does not match the target
        if (LoadTo.Try(out var load) && load.location.ContextsMatchPrefix(Contexts) == false &&
            ctx is StrongBoundedContext<T> sbc) {
            //Either use the result from execution, or use the loading default
            var lctx = load.replayer.TryGetChainedData<T>(Contexts);
            void Finish() {
                sbc.OnFinish?.Invoke();
                sbc.ShortCircuit?.Invoke();
                //Hierarchical data transfer: copy skipped BCTXData from the proxy/replayer load info
                // into the proxied/blank load info.
                //This includes the BCTX result as well as any saved locals.
                //If custom save data unhandled by VNState is modified, then you cannot use StrongBoundedContext.
                if (lctx != null)
                    if (Contexts.Count > 1)
                        Contexts[^2].Data.SaveNested(lctx, true);
                    else
                        InstanceData.SaveBCtxData(lctx, true);
            }
            if (lctx?.Result is {Valid: true, Value: {} res}) {
                Logs.Log($"Load-skipping section {ContextsDescriptor} with return value {res}");
                Finish();
                return res;
            } else if (sbc.LoadingDefault.Try(out res)) {
                Logs.Log($"Load-skipping section {ContextsDescriptor} with default return {res}");
                Finish();
                return res;
            }  else if (typeof(T) == typeof(Unit) && DefaultLoadSkipUnit) {
                Logs.Log($"Load-skipping section {ContextsDescriptor} with default Unit return");
                Finish();
                return default!;
            } else
                throw new Exception($"Requested to load-skip context {ContextsDescriptor}, " +
                                    "but save data does not have corresponding information");
        }
        var result = await innerTask();
        if (ctx is StrongBoundedContext<T> sbc_)
            sbc_.OnFinish?.Invoke();
        Logs.Log($"Saving result {result} to context {ContextsDescriptor}");
        openCtx.Data.Result = result;
        InstanceDataChanged.OnNext(InstanceData);
        return result;
    }

    /// <inheritdoc/>
    public bool TryGetContextData<T>(out BoundedContextData<T> value, params string[] contextKeys) =>
        (value = InstanceData.TryGetChainedData<T>(contextKeys)!) != null;

    /// <summary>
    /// Get the local data for the <see cref="IBoundedContext"/> with the provided parentage path,
    /// or throw an exception if it is not found.
    /// </summary>
    public BoundedContextData GetContextData(params string[] contextKeys) =>
        InstanceData.TryGetChainedData(contextKeys) ?? 
        throw new Exception($"No context data for {string.Join("::", contextKeys)}");

    /// <summary>
    /// Get the local data for the <see cref="BoundedContext{T}"/> with the provided parentage path,
    /// or throw an exception if it is not found.
    /// </summary>
    public BoundedContextData<T> GetContextData<T>(params string[] contextKeys) =>
        GetContextData(contextKeys).CastTo<T>();

    /// <summary>
    /// Get the saved result (ie. return value) of the context with the provided ID list.
    /// <br/>Will throw if the variable is not assigned.
    /// </summary>
    public T GetContextResult<T>(params string[] contextKeys) =>
        GetContextData<T>(contextKeys).Result.Try(out var res) ?
            res :
            throw new Exception($"Context {string.Join("::", contextKeys)} is unfinished");

    /// <summary>
    /// Try to get a saved local variable assigned to the context with the provided ID list.
    /// </summary>
    public bool TryGetContextValue<T>(string varName, out T value, params string[] contextKeys) {
        value = default!;
        return InstanceData.TryGetChainedData(contextKeys) is { } data && 
               data.Locals.TryGetData<T>(varName, out value);
    }

    /// <summary>
    /// Get a saved local variable assigned to the context with the provided ID list.
    /// <br/>Will throw if the variable is not assigned.
    /// </summary>
    public T GetContextValue<T>(string varName, params string[] contextIDs) =>
        GetContextData(contextIDs).Locals.GetData<T>(varName);

    /// <summary>
    /// Try to get a saved variable assigned to the current context.
    /// </summary>
    public bool TryGetLocalValue<T>(string varName, out T value) =>
        Contexts[^1].Data.Locals.TryGetData(varName, out value);

    /// <summary>
    /// Get a saved variable assigned to the current context.
    /// <br/>Will throw if the variable is not assigned.
    /// </summary>
    public T GetLocalValue<T>(string varName) =>
        Contexts[^1].Data.Locals.GetData<T>(varName);
    
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
    /// WARNING: THIS FUNCTION MAY NOT BE SAFE TO CALL WITHIN A BOUNDEDCONTEXT.
    ///  EDITING OTHER CONTEXTS' DATA MAY CAUSE ISSUES WITH LOADING.
    /// Save a variable to the context with the provided ID list. It can be accessed via
    /// <see cref="GetContextValue{T}"/> or <see cref="GetLocalValue{T}"/>.
    /// </summary>
    public void SaveContextValue<T>(string varName, T value, params string[] contextIDs) {
        (InstanceData.TryGetChainedData<T>(contextIDs) ??
            throw new Exception($"No context exists by keys {string.Join("::", contextIDs)}"))
            .Locals.SaveData(varName, value);
        InstanceDataChanged.OnNext(InstanceData);
    }

    /// <summary>
    /// Save a variable to the current bounded context. It can be accessed via
    /// <see cref="GetContextValue{T}"/> or <see cref="GetLocalValue{T}"/>.
    /// </summary>
    public void SaveLocalValue<T>(string varName, T value) {
        Contexts[^1].Data.Locals.SaveData(varName, value);
        InstanceDataChanged.OnNext(InstanceData);
    }

    /// <summary>
    /// Get the boolean result of a bounded context.
    /// <br/>Alias for <see cref="GetContextValue{T}"/> with T=bool
    /// </summary>
    public bool GetFlag(params string[] contextIDs) => GetContextResult<bool>(contextIDs);


    /// <inheritdoc/>
    public void Flush() => Update(0);
    
    /// <inheritdoc/>
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
    
    /// <summary>
    /// Find an entity of type T, or throw.
    /// </summary>
    public T Find<T>() {
        for (int ii = 0; ii < Entities.Count; ++ii)
            if (Entities.ExistsAt(ii) && Entities[ii] is T obj)
                return obj;
        throw new Exception($"Couldn't find entity of type {typeof(T)}");
    }
    /// <summary>
    /// Find an entity of type T, or return null.
    /// </summary>
    public T? FindEntity<T>() where T : class {
        for (int ii = 0; ii < Entities.Count; ++ii)
            if (Entities.ExistsAt(ii) && Entities[ii] is T obj)
                return obj;
        return null;
    }
    /// <summary>
    /// Find an entity of the provided type, or return null.
    /// </summary>
    public object? FindEntity(Type t) {
        for (int ii = 0; ii < Entities.Count; ++ii)
            if (Entities.ExistsAt(ii) && Entities[ii].GetType().IsWeakSubclassOf(t))
                return Entities[ii];
        return null;
    }
    /// <summary>
    /// Find all entities of the provided type.
    /// </summary>
    public List<T> FindEntities<T>() {
        var results = new List<T>();
        for (int ii = 0; ii < Entities.Count; ++ii)
            if (Entities.ExistsAt(ii) && Entities[ii] is T obj)
                results.Add(obj);
        return results;
    }

    /// <inheritdoc/>
    public C Add<C>(C ent, RenderGroup? renderGroup = null, int? sortingID = null) where C : IEntity {
        if (ent is RenderGroup rg) {
            _AddRenderGroup(rg);
            return ent;
        }
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

    private IDisposable _AddRenderGroup(RenderGroup rg) {
        if (RenderGroups.Any(x => x.Priority == rg.Priority.Value))
            throw new Exception("Cannot have multiple render groups with the same priority");
        var dsp = RenderGroups.Add(rg);
        (rg as IEntity).AddToVNState(this, dsp);
        _RenderGroupCreated.OnNext(rg);
        return dsp;
    }
    
    /// <summary>
    /// Create a <see cref="VNOperation"/> that waits for the given amount of time.
    /// </summary>
    public VNOperation Wait(float time) {
        return new VNOperation(this.AssertActive(), cT => {
            Run(WaitingUtils.WaitFor(time, WaitingUtils.GetCompletionAwaiter(out var t), cT, () => dT));
            return t;
        });
    }

    /// <summary>
    /// Create a <see cref="VNOperation"/> that waits until the condition is satisfied.
    /// </summary>
    public VNOperation Wait(Func<bool> condition) {
        return new VNOperation(this.AssertActive(), cT => {
            Run(WaitingUtils.WaitFor(condition, WaitingUtils.GetCompletionAwaiter(out var t), cT));
            return t;
        });
    }

    //TODO make this take cT -> Task<T>
    /// <inheritdoc/>
    public async Task<T> WrapExternal<T>(Task<T> task) {
        while (true) {
            var vnop = Wait(() => task.IsCompleted);
            var completion = await vnop;
            if (completion == Completion.Standard)
                return task.Result;
            if (completion == Completion.Cancelled)
                throw new OperationCanceledException();
            if (SkippingMode != null)
                SetSkipMode(null);
        }
    }

    /// <inheritdoc/>
    public void Run(IEnumerator ienum) {
        this.AssertActive();
        cors.Run(ienum,
            //Correct for the one-frame delay created by TryStepPrepend if this is called after VN finishes its cors.step
            new CoroutineOptions(ExecType: vnUpdated ? CoroutineType.StepTryPrepend : CoroutineType.TryStepPrepend));
    }

    /// <summary>
    /// Run multiple <see cref="ILazyAwaitable"/>s in parallel.
    /// </summary>
    public ILazyAwaitable Parallel(params ILazyAwaitable[] tasks) => new ParallelLazyAwaitable(tasks);

    /// <summary>
    /// Run multiple <see cref="ILazyAwaitable"/>s in sequence.
    /// </summary>
    public ILazyAwaitable Sequential(params ILazyAwaitable[] tasks) => new SequentialLazyAwaitable(tasks);

    /// <inheritdoc/>
    public IVNInterruptionToken Interrupt() => new InterruptionToken(this, CurrentInterrupt);

    private class InterruptionToken : IVNInterruptionToken {
        private VNState VN { get; }
        private VNInterruptionLayer Interruptee { get; }
        public InterruptionStatus? FinalStatus { get; private set; }
        public InterruptionToken(VNState vn, VNInterruptionLayer interruptee) {
            this.VN = vn;
            Interruptee = interruptee;
            interruptee.InterruptedBy = this;
            interruptee.Status = InterruptionStatus.Interrupted;
            if (interruptee.CurrentProcesses is { } pg)
                pg.LastInterruption = this;
            VN.Flush();
            var newLayer = new VNInterruptionLayer(VN, Interruptee);
            VN.interrupts.Push(newLayer);
            VN.InterruptionStarted.OnNext(newLayer);
        }

        public void ReturnInterrupt(InterruptionStatus resultStatus) {
            if (resultStatus is not (InterruptionStatus.Continue or InterruptionStatus.Abort))
                throw new Exception("Interruption return must either be CONTINUE or ABORT");
            if (FinalStatus != null)
                throw new Exception("Tried to return from the same interruption twice");
            if (VN.interrupts.Peek().Parent != Interruptee)
                throw new Exception(
                    "The process layer enclosing the current one is not the one that made the interruption");
            if (Interruptee.InterruptedBy != this)
                throw new Exception("Returning from an old interruption");
            FinalStatus = resultStatus;
            Interruptee.Status = resultStatus;
            Interruptee.InterruptedBy = null;
            VN.InterruptionEnded.OnNext(VN.interrupts.Pop());
        }
    }
    
    /// <inheritdoc/>
    public VNConfirmTask SpinUntilConfirm(VNOperation? preceding = null) {
        this.AssertActive();
        return new VNConfirmTask(this, preceding, op => {
            if (SkippingMode == SkipMode.LOADING)
                return Task.FromResult(Completion.SoftSkip);
            var awaiter = WaitingUtils.GetCompletionAwaiter(out var t);
            //Use StrongCancellee since we don't respect soft-skips (from Fastforward or from interruption)
            //op implicitly contains the VN CToken
            var cT = JointCancellee.From(new StrongCancellee(op), op.AwaitConfirm());
            Run(
                //If an confirm is interrupted, then when it returns to execution,
                // the confirm should be skipped and the next operation should be run.
                WaitingUtils.WaitFor(() => 
                        op.LastInterruption is { FinalStatus: { } } ||
                        //In cases where we are waiting on an interruption, we want the interruption to always finish first.
                        //As such, we raise the ctoken checking to the condition and disallow the case where
                        // CToken (VN lifetime token) is cancelled && last-interruption is ongoing.
                        cT.Cancelled && (!CToken.Cancelled || op.LastInterruption is null), _ => {
                        awaiter(cT.ToCompletion());
                        op.DoConfirm(); //in case we went through interruption shortcut
                        _awaitingConfirm.Recompute();
                    }, Cancellable.Null));
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

    /// <inheritdoc/>
    public bool UserConfirm() {
        this.AssertActive();
        if (interrupts.TryPeek(out var op) && op.ConfirmToken != null) {
            //User confirm cancels autoskip
            if (SkippingMode.IsPlayerControlled()) {
                Logs.Log($"Cancelling skip mode {SkippingMode} due to user confirm input.");
                SetSkipMode(null);
            }
            _Confirm();
            return true;
        } else
            return false;
    }

    /// <inheritdoc/>
    public void SkipOperation() {
        CurrentProcesses?.OperationCTS.Cancel(ICancellee.SoftSkipLevel);
    }
    
    /// <inheritdoc/>
    public bool RequestSkipOperation() {
        this.AssertActive();
        //User skip cancels autoskip
        if (SkippingMode.IsPlayerControlled()) {
            Logs.Log($"Cancelling skip mode {SkippingMode} due to user skip input.");
            SetSkipMode(null);
        }
        if (CurrentProcesses?.userSkipAllowed is true) {
            SkipOperation();
            return true;
        } else
            return false;
    }

    /// <inheritdoc/>
    public void RecordCG(IGalleryable cg) {
        InstanceData.GlobalData.GalleryCGViewed(cg.Key);
    }

    /// <summary>
    /// Inner handler called by <see cref="DeleteAll"/> to destroy all dependencies of this object.
    /// </summary>
    /// <exception cref="Exception"></exception>
    protected virtual void _DeleteAll() {
        Logs.Log($"Deleting all entities within VNState {this}");
        for (int ii = 0; ii < RenderGroups.Count; ++ii)
            if (RenderGroups.ExistsAt(ii))
                RenderGroups[ii].PreDelete();
        for (int ii = 0; ii < Entities.Count; ++ii)
            if (Entities.ExistsAt(ii))
                Entities[ii].PreDelete();
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
                                $"Script {ContextsDescriptor} has {RenderGroups.Count} remaining.");
        for (int ii = 0; ii < Entities.Count; ++ii) {
            if (Entities.ExistsAt(ii))
                Entities[ii].Delete();
        }
        Entities.Compact();
        if (Entities.Count > 0)
            throw new Exception("Some VNState entities were not deleted in the cull process. " +
                                $"Script {ContextsDescriptor} has {Entities.Count} remaining.");
        cors.CloseRepeated();
        if (cors.Count > 0)
            throw new Exception($"Some VNState coroutines were not closed in the cull process. " +
                                $"Script {ContextsDescriptor} has {cors.Count} remaining.");
        VNStateActive.OnNext(false);
    }
    
    //This is separate from EntityVNState, which is not set until DeleteAll ends
    private bool deleteStarted = false;
    /// <inheritdoc/>
    public void DeleteAll() {
        if (deleteStarted || VNStateActive == false) return;
        deleteStarted = true;
        _DeleteAll();
    }

    /// <inheritdoc/>
    public IInstanceData UpdateInstanceData() {
        InstanceData.Location = VNLocation.Make(this);
        return InstanceData;
    }


    /// <inheritdoc/>
    public virtual void OpenLog() {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public virtual void PauseGameplay() {
        throw new NotImplementedException();
    }
}
}