using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using JetBrains.Annotations;
using static BagoumLib.Expressions.VisitorHelpers;

namespace BagoumLib.Expressions {
/// <summary>
/// Converts an expression into C# source code using <see cref="LinearizeVisitor"/> and <see cref="PrintVisitor"/>.
/// </summary>
[PublicAPI]
public class ExpressionPrinter {
    /// <summary>
    /// Mumber of tabs with which to start printing.
    /// </summary>
    public int InitialIndent { get; set; } = 0;
    /// <summary>
    /// Number of spaces that one tab corresponds to.
    /// </summary>
    public int TabToSpace { get; set; } = 4;
    /// <summary>
    /// Printer that can convert types into strings.
    /// </summary>
    public ITypePrinter TypePrinter { get; set; } = new CSharpTypePrinter();
    /// <summary>
    /// Printer that can convert simple objects into strings.
    /// </summary>
    public IObjectPrinter ObjectPrinter { get; set; } = new CSharpObjectPrinter();
    /// <summary>
    /// Set this to true if you expect exceptions to arise unexpectedly. This will
    ///  use more temporary variables to ensure the correct order of exception throwing.
    /// </summary>
    public bool SafeLinearize { get; set; } = false;
    
    /// <summary>
    /// Linearizes an expression and then converts it into C# source code.
    /// </summary>
    public string LinearizePrint(Expression e) => Print(Linearize(e));

    /// <summary>
    /// Linearizes an expression using LinearizeVisitor.
    /// </summary>
    public Expression Linearize(Expression e) => new LinearizeVisitor() {
        SafeExecution = SafeLinearize
    }.Visit(e);

    
    /// <summary>
    /// Runs PrintVisitor on an expression and then converts the output into C# source code.
    /// <br/>You will probably need to run Linearize first for complex expressions.
    /// </summary>
    public string Print(Expression e) => Stringify(new PrintVisitor().Print(e));
    
    /// <summary>
    /// Converts an array of PrintTokens generated from PrintVisitor into C# source code.
    /// </summary>
    public string Stringify(PrintToken[] tokens) {
        var sb = new StringBuilder();
        var indent = InitialIndent;
        sb.Append(' ', indent * TabToSpace);
        var idCtr = 0;
        //Avoiding parameter duplication in the output
        Dictionary<object, string> objNames = new();
        Dictionary<string, object> nameObjs = new();
        void AddNewline() {
            sb.Append('\n');
            sb.Append(' ', indent * TabToSpace);
        }
        void Add(string s) => sb.Append(s);
        void AddNamed(object o, string? preferred, string prefix) {
            if (objNames.TryGetValue(o, out var s)) 
                Add(s);
            else {
                var newName = (preferred != null && !nameObjs.ContainsKey(preferred)) ?
                                preferred : 
                                $"{prefix}{idCtr++}";
                Add(objNames[o] = newName);
                nameObjs[newName] = o;
            }
        }
        void AddType(Type t) => Add(TypePrinter.Print(t));
        for(int ii = 0; ii < tokens.Length; ++ii) {
            var token = tokens[ii];
            switch (token) {
                case PrintToken.Constant constant:
                    Add(ObjectPrinter.Print(constant.value));
                    break;
                case PrintToken.Dedent:
                    --indent;
                    break;
                case PrintToken.Indent:
                    ++indent;
                    break;
                case PrintToken.Label label:
                    AddNamed(label.label, label.label.Name, "label_");
                    break;
                case PrintToken.Newline:
                    if (ii + 1 >= tokens.Length || tokens[ii + 1] is not PrintToken.UndoNewline)
                        AddNewline();
                    break;
                case PrintToken.UndoNewline:
                    break;
                case PrintToken.Parameter parameter:
                    var defaultTypeName = NameTypeInWords(parameter.ex.Type);
                    if (!defaultTypeName.All(char.IsLetterOrDigit))
                        defaultTypeName = "prm";
                    AddNamed(parameter.ex, parameter.ex.Name, defaultTypeName);
                    break;
                case PrintToken.Semicolon:
                    Add(";");
                    break;
                case PrintToken.Text text:
                    Add(text.String);
                    break;
                case PrintToken.TypeName typeName:
                    AddType(typeName.t);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(token));
            }
        }
        return sb.ToString();
    }
}
}