using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Compiler;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length > 0)
        {
            try
            {
                Process currentProgress = Process.GetCurrentProcess();
                long memoryBefore = currentProgress.WorkingSet64;
                
                var bytes = await InMemoryCompiler.Compile(args[0]);
                var result = await InMemoryCompiler.RunInMemoryAsync(bytes);
                
                currentProgress.Refresh();
                long memoryAfterExecution = currentProgress.WorkingSet64;
                
                // Print results in a single operation to reduce console writes
                result.memory = (memoryAfterExecution - memoryBefore) / (1024 * 1024);
                Console.WriteLine(JsonSerializer.Serialize(result));
                
            }
            catch (Exception e)
            {
                var result = new CompilerResultImpl
                {
                    timeTaken = -1,
                    output = e.Message,
                    isSuccess = false,
                    memory = -1
                };
                Console.WriteLine(JsonSerializer.Serialize(result));
            }
        }
        else
        {
            var result = new CompilerResultImpl
            {
                timeTaken = -1,
                output = "Please provide a source code file as an argument.",
                isSuccess = false,
                memory = -1
            };
            Console.WriteLine(JsonSerializer.Serialize(result));        }
    }
}