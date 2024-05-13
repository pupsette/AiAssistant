using Assistant.Core.Capabilities;
using OpenAI;
using OpenAI.Threads;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Assistant.Core
{
    public class OpenAiAssistant
    {
        private readonly string instructions;
        private readonly FunctionManager functionManager;
        private readonly string name;
        private readonly string contentOfAssistantHash;
        private readonly OpenAIClient client;
        private string? id;
        private readonly SemaphoreSlim initializationSync = new SemaphoreSlim(1);

        public OpenAiAssistant(OpenAIClient client, string name, string instructions, FunctionManager functionManager)
        {
            this.client = client;
            this.functionManager = functionManager;
            this.name = name;
            this.instructions = instructions;
            contentOfAssistantHash = GetContentHash(instructions, functionManager.GetAllOpenAiFunctions());
        }

        public async Task EnsureInitialized(CancellationToken cancellationToken)
        {
            if (id != null)
                return;

            await initializationSync.WaitAsync(cancellationToken);
            try
            {
                if (id != null)
                    return;

                ListResponse<OpenAI.Assistants.AssistantResponse> list = await client.AssistantsEndpoint.ListAssistantsAsync(null, cancellationToken);
                OpenAI.Assistants.AssistantResponse? assistant = list.Items.FirstOrDefault(i => i.Name?.ToLowerInvariant() == name.ToLowerInvariant());

                var request = new OpenAI.Assistants.CreateAssistantRequest(name: name, instructions: instructions, tools: functionManager.GetAllOpenAiFunctions().Select(f => new Tool(f)));
                if (assistant == null)
                {
                    id = (await client.AssistantsEndpoint.CreateAssistantAsync(request, cancellationToken)).Id;
                }
                else if (GetContentHash(assistant.Instructions, assistant.Tools.Where(t => t.Function != null).Select(t => t.Function).ToArray()) != contentOfAssistantHash)
                {
                    await client.AssistantsEndpoint.ModifyAssistantAsync(assistant!.Id, request, cancellationToken);
                    id = assistant.Id;
                }
            }
            finally
            {
                initializationSync.Release();
            }
        }

        public async Task<string> ContinueThread(string threadId, string message, CancellationToken cancellationToken)
        {
            await EnsureInitialized(cancellationToken);

            await client.ThreadsEndpoint.CreateMessageAsync(threadId, new CreateMessageRequest(message), cancellationToken);

            RunResponse run = await client.ThreadsEndpoint.CreateRunAsync(threadId, new CreateRunRequest(id), cancellationToken);
            run = await ProcessRunAsync(run, cancellationToken);
            MessageResponse? response = (await run.ListMessagesAsync(new ListQuery(1), cancellationToken)).Items.SingleOrDefault();
            if (response == null)
                return "NO RESPONSE";

            StringBuilder sb = new StringBuilder();
            foreach (Content? content in response.Content)
                sb.Append(content.Text.Value ?? "...");
            return sb.ToString();
        }

        public async Task<string> StartNewThread(CancellationToken cancellationToken)
        {
            await EnsureInitialized(cancellationToken);

            ThreadResponse response = await client.ThreadsEndpoint.CreateThreadAsync(null, cancellationToken);
            return response.Id;
        }

        private async Task<RunResponse> ProcessRunAsync(RunResponse run, CancellationToken cancellationToken)
        {
            while (run.Status is RunStatus.Queued or RunStatus.InProgress or RunStatus.Cancelling or RunStatus.RequiresAction)
            {
                if (run.Status == RunStatus.RequiresAction)
                {
                    IReadOnlyList<ToolCall>? calls = run.RequiredAction?.SubmitToolOutputs?.ToolCalls;
                    if (calls == null)
                    {
                        await run.CancelAsync();
                        return run;
                    }

                    List<ToolOutput> toolOutputs = new(calls.Count);
                    foreach (ToolCall call in calls)
                    {
                        toolOutputs.Add(new ToolOutput(
                        call.Id,
                            await functionManager.CallAsync(call.FunctionCall.Name, call.FunctionCall.Arguments, cancellationToken)));
                    }
                    await run.SubmitToolOutputsAsync((IEnumerable<ToolOutput>)toolOutputs, cancellationToken);
                }
                await Task.Delay(100);
                run = await run.UpdateAsync(cancellationToken);
            }
            return run;
        }

        private static string GetContentHash(string instructions, Function[] functions)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(instructions);
            foreach (var function in functions)
            {
                sb.AppendLine(function.Name);
                sb.AppendLine(function.Description);
                sb.AppendLine(function.Parameters?.ToJsonString() ?? "-");
            }

            byte[] hash = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));

            sb.Clear();
            for (int i = 0; i < hash.Length; i++)
                sb.AppendFormat("{0:X2}", hash[i]);

            return sb.ToString();
        }
    }
}
