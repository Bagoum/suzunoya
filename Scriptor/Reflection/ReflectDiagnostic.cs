using BagoumLib;
using Mizuhashi;

namespace Scriptor.Reflection;

//This class should eventually replace direct usage of exceptions in reflection.
// For now, I am only using it for warnings.
public abstract record ReflectDiagnostic(PositionRange Position, string Message, ReflectDiagnostic? Inner = null) {
    protected virtual LogLevel Level => LogLevel.INFO;  
    public void Log(){
        Logging.Logs.Log($"{Position}: {Message}", true, Level);
    }
    public record Warning(PositionRange Position, string Message, ReflectDiagnostic? Inner = null) : ReflectDiagnostic(Position, Message, Inner) {
        protected override LogLevel Level => LogLevel.WARNING;
    }

}