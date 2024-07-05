using System;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using BagoumLib.Events;
using BagoumLib.Tasks;

namespace Suzunoya.ADV {

/// <summary>
/// Safely manages transitions between data states operated over by <see cref="MapStateManager{I,D}"/>.
/// </summary>
public class MapStateTransition<I, D> where I: ADVIdealizedState where D: ADVData {
    /// <summary>
    /// Handler for maps and assertions.
    /// </summary>
    public MapStateManager<I, D> MapStates { get; }

    private readonly TaskQueue taskQueue = new();

    /// <summary>
    /// True when the map state is changing.
    /// <br/>Consumers may want to disable certain functionalities while this is true.
    /// </summary>
    public Evented<bool> ExecutingTransition => taskQueue.ExecutingTransition;
    
    /// <summary>
    /// The task describing the current map update, if a map update is occuring.
    /// </summary>
    public Task? MapUpdateTask => taskQueue.CurrentTask;
    
    //Note: you'll have to do some plumbing to ensure that this enqueue mechanism doesn't get clogged
    // when teleporting using the world map while an update is occuring.
    //Pushing a vn skip operation or two should work.
    public MapStateTransition(MapStateManager<I, D> mapStates) {
        this.MapStates = mapStates;
    }

    /// <summary>
    /// Update the map data, triggering a transition (that may be delayed if one is already executing)
    ///  to the new data state.
    /// </summary>
    public Task UpdateMapData(D nextData, MapStateTransitionSettings<I>? settings) {
        return taskQueue.EnqueueTask(() => MapStates.UpdateMaps(nextData, nextData.CurrentMap, settings));
    }
}
}