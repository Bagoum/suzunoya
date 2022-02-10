
//This file contains interfaces for task handlers not within the scope of the Suzunoya VNState.
// Most importantly, it contains an interface for a player choice handler.
// It is recommended, but not required, to implement these interfaces for external task handlers.
// As long as external tasks are wrapped in BoundedContext or LockedContext, they should cause
// no issues with save/load.

namespace Suzunoya.ControlFlow {


public interface IChoiceAsker {
    BoundedContext<T> Ask<T>(string key, (T value, string description)[] options);
}


public interface IExternalTask<T> {
    BoundedContext<T> Execute(string key);
}


}