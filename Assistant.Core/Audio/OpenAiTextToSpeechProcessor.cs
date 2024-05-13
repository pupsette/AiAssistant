using NAudio.Wave;
using OpenAI;

namespace Assistant.Core.Audio
{
    public class OpenAiTextToSpeechProcessor
    {
        private readonly OpenAIClient client;

        public OpenAiTextToSpeechProcessor(OpenAIClient client)
        {
            this.client = client;
        }

        public async Task PlayAsync(string text, CancellationToken cancellationToken, OpenAI.Audio.SpeechVoice voice = OpenAI.Audio.SpeechVoice.Alloy)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var response = await client.AudioEndpoint.CreateSpeechAsync(new OpenAI.Audio.SpeechRequest(text, voice: voice), cancellationToken: cancellationToken);
            await PlayMP3FromStreamAsync(new MemoryStream(response.ToArray()), cancellationToken);
        }

        static async Task PlayMP3FromStreamAsync(Stream mp3Content, CancellationToken cancellationToken)
        {
            // Create a WaveStream from the memory stream
            using (Mp3FileReader waveStream = new Mp3FileReader(mp3Content))
            {
                // Use WaveOut to play the WaveStream
                using (WaveOutEvent waveOut = new WaveOutEvent())
                {
                    waveOut.Init(waveStream);
                    waveOut.Play();

                    // Wait for playback to finish
                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        await Task.Delay(10);
                        if (cancellationToken.IsCancellationRequested)
                        {
                            waveOut.Stop();
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                }
            }
        }
    }
}