using OpenAI;
using System.ComponentModel;
using System.Reflection;

namespace Assistant.Core.Capabilities
{
    public class FunctionManager : IFunctionExecutor
    {
        private readonly Dictionary<string, FunctionWrapper> functions = new Dictionary<string, FunctionWrapper>();

        public Task<string> CallAsync(string name, string argumentsJson, CancellationToken cancellationToken)
        {
            return functions[name].ExecutorAsync(argumentsJson, cancellationToken);
        }

        public void AddFunctions(object instance)
        {
            Type type = instance.GetType();
            foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public))
            {
                if (TryConvert(instance, member, out FunctionWrapper? wrapper))
                    functions.Add(wrapper!.Name, wrapper);
            }
        }

        public Function[] GetAllOpenAiFunctions()
        {
            return functions.Values.Select(fw => fw.OpenAiFunction).ToArray();
        }

        private static bool TryConvert(object instance, MemberInfo member, out FunctionWrapper? wrapper)
        {
            wrapper = default;
            if (member is not MethodInfo mi)
                return false;

            DescriptionAttribute? di = mi.GetCustomAttribute<DescriptionAttribute>();
            if (di == null)
                return false;

            ParameterInfo[] parameters = mi.GetParameters();
            if (parameters.Length > 1)
                return false;

            string? argumentsJson = null;
            Type? parameterType = null;
            if (parameters.Length == 1)
            {
                parameterType = parameters[0].ParameterType;
                argumentsJson = NJsonSchema.Generation.JsonSchemaGenerator.FromType(parameterType, new NJsonSchema.Generation.SystemTextJsonSchemaGeneratorSettings()).ToJson();
            }

            wrapper = new FunctionWrapper(mi.Name, di.Description, argumentsJson, (str, token) =>
                {
                    try
                    {
                        object?[]? parameters = [];
                        if (parameterType != null)
                            parameters = [System.Text.Json.JsonSerializer.Deserialize(str, parameterType)];

                        Console.WriteLine($"Calling {mi.Name}({str})");

                        return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(mi.Invoke(instance, parameters)));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed: {ex}");

                        return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(new ErrorResponse(ex.Message)));
                    }
                });
            return true;
        }

        private class FunctionWrapper
        {
            public FunctionWrapper(string name, string description, string? parametersJson, Func<string, CancellationToken, Task<string>> executorAsync)
            {
                Name = name;
                Description = description;
                ParametersJson = parametersJson;

                OpenAiFunction = new Function(name, description, parametersJson ?? "{}");
                ExecutorAsync = executorAsync;
            }

            public string Name { get; }
            public string Description { get; }
            public string? ParametersJson { get; }
            public Function OpenAiFunction { get; }
            public Func<string, CancellationToken, Task<string>> ExecutorAsync { get; }
        }

        private class ErrorResponse
        {
            public ErrorResponse(string error)
            {
                FailureReason = error;
            }

            public string Instructions { get; set; } = "The tool failed to execute. Please stop further processing and report the failure reason.";

            public string FailureReason { get; set; }
        }
    }
}
