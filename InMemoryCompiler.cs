using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.Emit;

namespace Compiler;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public interface CompilerResult
{
    int timeTaken { get; set; }
    string output { get; set; }
    bool isSuccess { get; set; }
    double? memory { get; set; }
}
public class CompilerResultImpl : CompilerResult
{
    public int timeTaken { get; set; }
    public string output { get; set; }
    public bool isSuccess { get; set; }
    public double? memory { get; set; }
}

public class CompilationException(string message) : Exception(message);
public class InMemoryCompiler
{
    public static async Task<byte[]> Compile(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var metaReferences = DynamicReferenceResolverService.DiscoverAll(source);
        // 1. Create a compilation object
        var compilation = CSharpCompilation.Create("MyFirstAssembly",
            syntaxTrees: [syntaxTree],
            references: metaReferences,
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithOptimizationLevel(OptimizationLevel.Release)
        );
        // 2. Using a MemoryStream to hold the compiled DLL

        await using var dllStream = new MemoryStream();

        EmitResult emitResult = compilation.Emit(dllStream);

        if (!emitResult.Success)
        {
            ReportDiagnostics(emitResult.Diagnostics);
            return [];
        }

        // 3. Return the bytes
        return dllStream.ToArray();
    }

    private static void ReportDiagnostics(IEnumerable<Diagnostic> diags)
    {
        foreach (Diagnostic d in diags.Where(d => d.Severity == DiagnosticSeverity.Error))
        {
            // Map line/column numbers to be nice to callers
            var lineSpan = d.Location.GetLineSpan();
            throw new CompilationException($"{lineSpan.Path}({lineSpan.StartLinePosition.Line + 1}," +
                                           $"{lineSpan.StartLinePosition.Character + 1}): {d.GetMessage()}");
        }
    }

    public static async Task SaveAssemblyAsync(
        byte[] dllBytes,
        string dllPath,
        string assemblyName = "MyFirstAssembly"
    )
    {
        if (dllBytes is null || dllBytes.Length == 0)
            throw new ArgumentException("Assembly byte array is empty.", nameof(dllBytes));

        // Write the DLL
        dllPath = Path.Combine(dllPath, $"{assemblyName}.dll");
        await File.WriteAllBytesAsync(dllPath, dllBytes);
    }

    
    public static async Task<CompilerResult> RunInMemoryAsync(byte[] dllBytes, string[]? args = null)
    {
        // Load assembly and get an entry point
        var asm = Assembly.Load(dllBytes);
        var entry = asm.EntryPoint
                    ?? throw new InvalidOperationException("No entry point found.");

        // Capture console output
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Invoke an entry point with or without args
            object? result = null;
            // Caculate time taken to execute code
            var stopwatch = Stopwatch.StartNew();
            if (entry.GetParameters().Length == 0)
                result = entry.Invoke(null, null);
            else
                result = entry.Invoke(null, new object?[] { args ?? Array.Empty<string>() });

            // Wait if a result is a Task
            if (result is Task task) await task;
            stopwatch.Stop();
            CompilerResult compilerResult = new CompilerResultImpl
            {
                timeTaken = (int)stopwatch.ElapsedMilliseconds,
                output = stringWriter.ToString(),
                isSuccess = true
            };
            return compilerResult;
        }
        finally
        {
            // Ensure console output is restored
            Console.SetOut(originalOut);
        }
    }
}

