using NAudio.Wave;
using OpenAI;

namespace Assistant.Core.Audio
{
    public unsafe class OpenAiSpeechToTextProcessor : IWaveSamplesProcessor
    {
        public OpenAiSpeechToTextProcessor(OpenAIClient client, string? language = null)
        {
            this.client = client;
            this.language = language;
        }

        public Action<string>? OnTextEvent;
        private WaveFormat? waveFormat;
        private readonly OpenAIClient client;
        private readonly string? language;

        public void Process(ReadOnlySpan<float> samples)
        {
            var stream = new MemoryStream();
            WaveFileWriter writer = new(stream);

            writer.Setup(waveFormat!);
            writer.Process(samples);
            writer.Finish();

            stream.Position = 0;

            client.AudioEndpoint.CreateTranscriptionJsonAsync(new OpenAI.Audio.AudioTranscriptionRequest(stream, "input.wav", language: language, responseFormat: OpenAI.Audio.AudioResponseFormat.Verbose_Json)).ContinueWith(t =>
            {
                OpenAI.Audio.AudioResponse response = t.Result;
                if (!response.Text.Any(char.IsAsciiLetter))
                    return;

                OnTextEvent?.Invoke(response.Text);
            });
        }

        public void Setup(WaveFormat waveFormat)
        {
            if (waveFormat == null)
                throw new ArgumentNullException(nameof(waveFormat));

            if (waveFormat.Channels != 1)
                throw new NotSupportedException("Only mono audio is supported.");

            this.waveFormat = waveFormat;
        }

        public void Finish()
        {
        }
    }
}