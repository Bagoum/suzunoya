using BagoumLib;
using Mizuhashi;

namespace Scriptor.Reflection;

//This class should eventually replace direct usage of exceptions in reflection.
// For now, I am only using it for warnings.
/// <summary>
/// A diagnostic message (possibly fatal) representing an issue or matter of note in script compilation.
/// </summary>
public abstract record ReflectDiagnostic(PositionRange Position, string Message, ReflectDiagnostic? Inner = null) {
    /// <summary>
    /// Message level.
    /// </summary>
    protected virtual LogLevel Level => LogLevel.INFO;  
    
    /// <summary>
    /// Log the message to <see cref="Logging.Logs"/>.
    /// </summary>
    public void Log(){
        Logging.Logs.Log($"{Position}: {Message}", true, Level);
    }
    
    /// <summary>
    /// A warning message.
    /// </summary>
    public record Warning(PositionRange Position, string Message, ReflectDiagnostic? Inner = null) : ReflectDiagnostic(Position, Message, Inner) {
        /// <inheritdoc/>
        protected override LogLevel Level => LogLevel.WARNING;
    }

}