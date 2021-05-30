using System.Threading;
using System.Threading.Tasks;
using Suzunoya.ControlFlow;

namespace Suzunoya {
public interface IScript {
    public IVNState State { get; }
    public Task Run();
}

}