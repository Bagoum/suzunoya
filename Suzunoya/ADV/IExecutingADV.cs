using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;

namespace Suzunoya.ADV {

/// <summary>
/// The process executing an ADV instance. This is subclassed for each game.
/// </summary>
public interface IExecutingADV : IDisposable {
    /// <inheritdoc cref="ADVInstance"/>
    ADVInstance Inst { get; }
    /// <inheritdoc cref="ADVInstance.ADVData"/>
    ADVData ADVData => Inst.ADVData;
    /// <inheritdoc cref="ADVInstance.Manager"/>
    ADVManager Manager => Inst.Manager;
    /// <inheritdoc cref="ADVInstance.VN"/>
    IVNState VN => Inst.VN;
    /// <inheritdoc cref="MapStateManager{I,D}"/>
    IMapStateManager MapStates { get; }
    /// <summary>
    /// Run the ADV. Returns an <see cref="IADVCompletion"/> when the entirety of the ADV is complete.
    /// </summary>
    Task<IADVCompletion> Run();
}

/// <summary>
/// See <see cref="IExecutingADV"/>
/// </summary>
/// <typeparam name="I">Type of idealized state container</typeparam>
/// <typeparam name="D">Type of save data</typeparam>
public interface IExecutingADV<I, D> : IExecutingADV where I : ADVIdealizedState where D : ADVData {
    /// <inheritdoc cref="MapStateManager{I,D}"/>
    new MapStateManager<I, D> MapStates { get; }
    /// <inheritdoc/>
    IMapStateManager IExecutingADV.MapStates => MapStates; 
}

/// <summary>
/// Baseline implementation of <see cref="IExecutingADV"/>
/// that can be used for pure VN games with no actual ADV functionality.
/// <br/>This handles load functionality if setup in <see cref="IADVInstanceRequest.LoadProxyData"/>.
/// </summary>
[PublicAPI]
public class BarebonesExecutingADV<D> : IExecutingADV<ADVIdealizedState, D> where D : ADVData {

    /// <inheritdoc/>
    public void Dispose() { }
    /// <inheritdoc/>
    public ADVInstance Inst { get; }
    private readonly Func<Task> executor;
    /// <inheritdoc/>
    public MapStateManager<ADVIdealizedState, D> MapStates { get; }
    
    /// <summary>
    /// </summary>
    public BarebonesExecutingADV(ADVInstance inst, Func<Task> executor) {
        this.Inst = inst;
        this.executor = executor;
        this.MapStates = new MapStateManager<ADVIdealizedState, D>(this, () => new(this));
    }

    /// <inheritdoc/>
    public async Task<IADVCompletion> Run() {
        if (Inst.Request.LoadProxyData?.VNData is { Location: { } l} replayer)
            Inst.VN.LoadToLocation(l, replayer, () => {
                Inst.Request.FinalizeProxyLoad();
            });
        await executor();
        return new UnitADVCompletion();
    }
}
}