using Assistant.CLI.Settings;
using Assistant.Core;
using Assistant.Core.Audio;
using Assistant.Core.Capabilities;
using NAudio.Wave;
using OpenAI;
using OpenAI.Threads;

namespace Assistant.CLI
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Hello!");

                //var processor = new SpeechToTextProcessor(GgmlType.Medium);
                //Console.Write("Loading whisper model...");
                //processor.WaitForReady();
                //processor.OnSegmentEvent += segment =>
                //{
                //    Console.WriteLine($"{segment.Probability:0.0} - {segment.Text}");
                //};
                //Console.WriteLine("loaded.");

                string? apiKey = AppSettings.Instance.OpenAi?.ApiKey;
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("API key for OpenAI is required.");
                    return;
                }
                var openAiClient = new OpenAIClient(new OpenAIAuthentication(apiKey));
                var speechToText = new OpenAiSpeechToTextProcessor(openAiClient, "de");
                var textToSpeech = new OpenAiTextToSpeechProcessor(openAiClient);

                var multi = new MultiProcessor(/*new Core.Audio.WaveFileWriter("out.wav", true), */speechToText);

                var chunker = new AudioChunker(multi, TimeSpan.FromSeconds(0.6), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6));

                var functionManager = new FunctionManager();
                functionManager.AddFunctions(new TimeFunctions());

                var assistant = new OpenAiAssistant(openAiClient, ".NET utility assistant",
@"You are a helpful assistant, answering my questions by using helpful tools or getting tasks done for me.
- You are not chatty!
- Try to be short and precise.
- You will answer in german, always.
- You don't respond to short text prompts. You just say nothing in such cases!
- You never say good bye. You just say nothing in such cases!
- If you are using a tool and it fails, please return the failure reason immediately and stop further assistance.
- If you are unsure or if you don't have enough information to respond properly, tell the user about your uncertainty and try to tell what the problem is.", functionManager);
                string threadId = await assistant.StartNewThread(CancellationToken.None);

                speechToText.OnTextEvent += t =>
                {
                    Console.WriteLine(t);
                    async Task Continue(CancellationToken cancellationToken)
                    {
                        string text = await assistant.ContinueThread(threadId, t, cancellationToken);
                        Console.WriteLine(text);
                        await textToSpeech.PlayAsync(text, cancellationToken, OpenAI.Audio.SpeechVoice.Nova);
                    }
                    Continue(CancellationToken.None);
                };

                var format = new WaveFormat(16000, 1);
                using var inputStream = new AudioWaveInputStream(chunker, format);
                Console.WriteLine("Recording audio... Press ESC to quit.");

                while (Console.ReadKey(true).Key != ConsoleKey.Escape)
                    continue;

                inputStream.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
