using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using JetBrains.Annotations;
using Suzunoya.ADV;
using Suzunoya.ControlFlow;

namespace Suzunoya.ADV {
/// <summary>
/// Contains all top-level metadata about an executing ADV instance that is not specific to the game.
/// <br/>The actual execution process is handled by a game-specific <see cref="IExecutingADV"/>.
/// </summary>
/// <param name="Request">Request data storing information required to start the ADV instance</param>
/// <param name="VN">VN state container that is persistent through the game, on which all bounded contexts are run</param>
/// <param name="Tracker">Cancellation token wrapping the ADV instance execution</param>
[PublicAPI]
public record ADVInstance(IADVInstanceRequest Request, IVNState VN, Cancellable Tracker) : IDisposable { 
    /// <summary>
    /// Game data describing the player's progress through the game.
    /// </summary>
    public ADVData ADVData => Request.ADVData;
    /// <inheritdoc cref="IADVInstanceRequest.Manager"/>
    public ADVManager Manager => Request.Manager;
    
    /// <summary>
    /// Cancel the cancellation token and destroy all contents of the executing VN.
    /// </summary>
    public void Cancel() {
        Tracker.Cancel();
        VN.DeleteAll(); //this cascades into destroying executingVN
    }

    /// <inheritdoc/>
    public void Dispose() => Cancel();
}


/// <summary>
/// Contains information necessary to start an ADV instance.
/// <br/>Once the instance is started, metadata such as the execution tracker
/// is stored in a constructed <see cref="ADVInstance"/>.
/// </summary>
[PublicAPI]
public interface IADVInstanceRequest {
    /// <inheritdoc cref="ADVManager"/>
    public ADVManager Manager { get; }
    /// <summary>
    /// Save data to load from.
    /// </summary>
    public ADVData ADVData { get; }
    
    /// <summary>
    /// When loading into an in-progress BoundedContext, this contains the "true" save data,
    ///  that is replayed onto the "blank" save data in <see cref="ADVData"/>.
    /// <br/>You can set this up by calling `(ADVData, LoadProxyData) = advData.GetLoadProxyInfo();` in the constructor.
    /// </summary>
    public ADVData? LoadProxyData { get; }

    /// <summary>
    /// After loading into an in-progress BoundedContext, call this method to swap <see cref="LoadProxyData"/>
    /// (the "true" save data) and <see cref="ADVData"/> (the "blank/replayee" save data).
    /// </summary>
    public void FinalizeProxyLoad();
    
    /// <summary>
    /// Enter the ADV scene and run the ADV instance.
    /// Returns false if the scene fails to load.
    /// </summary>
    public bool Run();

    /// <summary>
    /// Restart the ADV instance, possibly with an overriden data.
    /// </summary>
    public bool Restart(ADVData? data = null);
}
}