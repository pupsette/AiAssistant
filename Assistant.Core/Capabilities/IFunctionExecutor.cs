namespace Assistant.Core.Capabilities
{
    public interface IFunctionExecutor
    {
        Task<string> CallAsync(string name, string argumentsJson, CancellationToken cancellationToken);
    }
}
