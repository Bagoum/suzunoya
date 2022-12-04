using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Tasks;
using JetBrains.Annotations;
using Suzunoya.ADV;
using Suzunoya.ControlFlow;

namespace Suzunoya.ADV {

/// <summary>
/// Base implementation of <see cref="IExecutingADV"/> for ADV games requiring assertion logic and map controls.
/// <br/>Almost all game configuration is done in abstract method <see cref="ConfigureMapStates"/>.
/// </summary>
[PublicAPI]
public abstract class BaseExecutingADV<I, D> : IExecutingADV<I, D> where I : ADVIdealizedState where D : ADVData {
    /// <inheritdoc/>
    public ADVInstance Inst { get; }
    /// <inheritdoc/>
    public ADVManager Manager => Inst.Manager;
    /// <inheritdoc cref="ADVInstance.VN"/>
    public IVNState VN => Inst.VN;
    //Note that underlying data may change due to proxy loading
    /// <inheritdoc cref="ADVInstance.ADVData"/>
    protected D Data => (Inst.ADVData as D) ?? throw new Exception("Instance data is of wrong type");
    /// <summary>
    /// Event called immediately after save data is changed, and before assertions are recomputed.
    /// </summary>
    public Evented<D> DataChanged { get; }
    private string prevMap;
    /// <summary>
    /// Handler for managing maps and assertions.
    /// </summary>
    public MapStateManager<I, D> MapStates { get; }
    private readonly MapStateTransition<I, D> mapTransition;
    /// <inheritdoc cref="MapStateTransition{I,D}.MapUpdateTask"/>
    protected Task MapTransitionTask => mapTransition.MapUpdateTask ?? Task.CompletedTask;
    /// <summary>
    /// True when the current map is changing (eg. from Hakurei Shrine to Moriya Shrine).
    /// </summary>
    private readonly Evented<bool> executingCrossMapTransition = new(false);
    /// <summary>
    /// Task describing the output of the entire game. Set the result when the entire game is complete.
    /// </summary>
    protected readonly TaskCompletionSource<IADVCompletion> completion = new();
    /// <inheritdoc cref="ITokenized.Tokens"/>
    protected readonly List<IDisposable> tokens = new();
    private readonly List<IDisposable> transitionToken = new();
    private readonly List<IDisposable> mapLocalTokens = new();
    /// <summary>
    /// Maps a BCtx ID to the corresponding BCtx for all top-level BCtxes that can be loaded into
    /// </summary>
    protected readonly Dictionary<string, BoundedContext<Unit>> bctxes = new();
    /// <summary>
    /// Event called when the map is updated via <see cref="GoToMap"/>.
    /// <br/>It is called right before assertions are recomputed.
    /// </summary>
    protected readonly Evented<(string? prevMap, string newMap)> MapWillUpdate;

    /// <summary>
    /// Constructor for <see cref="BaseExecutingADV{I,D}"/>.
    /// <br/>Note that <see cref="ConfigureMapStates"/> is called in this constructor.
    /// </summary>
    public BaseExecutingADV(ADVInstance inst) {
        this.Inst = inst;
        prevMap = Data.CurrentMap;
        MapWillUpdate = new((null, prevMap));
        // ReSharper disable once VirtualMemberCallInConstructor
        MapStates = ConfigureMapStates();
        mapTransition = new(MapStates);
        tokens.Add(mapTransition.ExecutingTransition.Subscribe(b => {
            if (b)
                transitionToken.Add(Manager.ADVState.AddConst(ADVManager.State.Waiting));
            else
                transitionToken.DisposeAll();
        }));
        tokens.Add(MapStates.MapEndStateDeactualized.Subscribe(_ => mapLocalTokens.DisposeAll()));
        tokens.Add(VN.InstanceDataChanged.Subscribe(_ => UpdateDataV(_ => { })));
        DataChanged = new Evented<D>(Data);

        //Listen to common events
        VN.ContextStarted.Subscribe(c => {
            if (c.BCtx is IStrongBoundedContext { LoadSafe: false })
                Data.LockContext(c);
        });
        VN.ContextFinished.Subscribe(c => {
            if (c.BCtx is IStrongBoundedContext { LoadSafe: false })
                Data.UnlockContext(c);
        });
        
    }

    /// <summary>
    /// Set a disposable to be automatically disposed when the map changes.
    /// </summary>
    protected T DisposeWithMap<T>(T token) where T: IDisposable {
        mapLocalTokens.Add(token);
        return token;
    }
    
    /// <summary>
    /// Change the current map. Always call this method instead of setting <see cref="ADVData.CurrentMap"/>,
    ///  as it triggers <see cref="MapWillUpdate"/>.
    /// </summary>
    /// <param name="map">New map to go to</param>
    /// <param name="updater">Optional data update step that will run before the map change</param>
    /// <returns></returns>
    protected Task GoToMap(string map, Action<D>? updater = null) {
        var prev = Data.CurrentMap;
        if (prev != map) {
            return UpdateData(adv => {
                updater?.Invoke(adv);
                adv.CurrentMap = map;
                MapWillUpdate.OnNext((prev, map));
            });
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Update the game data and recompute map assertions. This returns nothing; use <see cref="UpdateData"/> if you
    ///  want to await the update task.
    /// </summary>
    protected void UpdateDataV(Action<D> updater, MapStateTransitionSettings<I>? transition = null) {
        _ = UpdateData(updater, transition).ContinueWithSync();
    }
    /// <summary>
    /// Update the game data and recompute map assertions.
    /// </summary>
    protected Task UpdateData(Action<D> updater, MapStateTransitionSettings<I>? transition = null) {
        updater(Data);
        DataChanged.OnNext(Data);
        return UpdateMap(transition);
    }

    //Function to change the map configuration. Runs whenever the game data changes
    private async Task UpdateMap(MapStateTransitionSettings<I>? transition) {
        if (Data.CurrentMap != prevMap) {
            prevMap = Data.CurrentMap;
            executingCrossMapTransition.OnNext(true);
            //Clear whatever existing update operation is occuring (TODO: test w/w/o this)
            Inst.VN.SkipOperation();
            Inst.VN.Flush();
            //Then push the map update operation
            await mapTransition.UpdateMapData(Data, transition);
            executingCrossMapTransition.OnNext(false);
        } else {
            //Not a CurrentMap transition
            await mapTransition.UpdateMapData(Data, transition);
        }
    }
    
    /// <summary>
    /// Use this proxy function to register BCTXs so they can be inspected and run on load.
    /// Top-level contexts should always be {Unit}.
    /// If you provide an unidentifiable id (eg. empty string), it won't be loadable.
    /// </summary>
    protected BoundedContext<Unit> Context(string id, Func<Task> innerTask) {
        var ctx = new BoundedContext<Unit>(VN, id, async () => {
            await innerTask();
            return default;
        });
        if (ctx.Identifiable) {
            if (bctxes.ContainsKey(ctx.ID))
                throw new Exception($"Multiple BCTXes are defined for key {ctx.ID}");
            bctxes[ctx.ID] = ctx;
        }
        return ctx;
    }
    
    /// <summary>
    /// Boilerplate code for running an ADV-based game. 
    /// </summary>
    public async Task<IADVCompletion> Run() {
        await mapTransition.UpdateMapData(Data, new MapStateTransitionSettings<I> {
            ExtraAssertions = (map, s) => {
                if (map == prevMap && Inst.Request.LoadProxyData?.VNData is { Location: { } l} replayer) {
                    //If saved during a VN segment, load into it
                    Inst.VN.LoadToLocation(l, replayer, () => {
                        Inst.Request.FinalizeProxyLoad();
                        UpdateDataV(_ => { });
                    });
                    if (!s.SetEntryVN(bctxes[l.Contexts[0]], RunOnEntryVNPriority.LOAD))
                        throw new Exception("Couldn't set load entry VN");
                }
            }
        });
        //This is when the entire game finishes
        return await completion.Task;
    }

    /// <summary>
    /// Map setup function run once during game initialization. This handles the game's entire logical configuration.
    /// <br/>Subclasses must override this to make data-dependent assertions on maps.
    /// <br/>Assertions will be re-evaluated whenever the instance data changes.
    /// <br/>Example usage, where SomeEntity appears on MyMapName after QuestYYY is accepted:
    /// <code>
    /// ms.ConfigureMap("MyMapName", (i, d) => {
    ///   if (d.QuestState.QuestYYY >= QuestState.ACCEPTED) {
    ///     i.Assert(new EntityAssertion&lt;SomeEntity&gt;(vn));
    /// ...
    /// </code>
    /// </summary>
    protected abstract MapStateManager<I, D> ConfigureMapStates();
    
    /// <inheritdoc/>
    public void Dispose() {
        tokens.DisposeAll();
        transitionToken.DisposeAll();
        mapLocalTokens.DisposeAll();
    }
}

}