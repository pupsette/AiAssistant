using NAudio.Wave;

namespace Assistant.Core.Audio
{
    public class AudioWaveInputStream : IDisposable
    {
        private readonly WaveInEvent waveIn;
        private readonly int channelCount;
        private bool isDisposed = false;
        private readonly IWaveSamplesProcessor processor;
        private readonly float[] buffer;
        private readonly object outputSync = new();

        public AudioWaveInputStream(IWaveSamplesProcessor processor, WaveFormat format)
        {
            this.processor = processor ?? throw new ArgumentNullException(nameof(processor));

            waveIn = new WaveInEvent();
            waveIn.WaveFormat = format;
            waveIn.DataAvailable += WaveIn_DataAvailable;

            channelCount = format.Channels;
            buffer = new float[channelCount * 4096];
            processor.Setup(format);

            waveIn.StartRecording();
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (isDisposed)
                return;

            try
            {
                lock (outputSync)
                {
                    int bufferIndex = 0;
                    for (int i = 0; i < e.BytesRecorded;)
                    {
                        for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
                        {
                            buffer[bufferIndex++] = BitConverter.ToInt16(e.Buffer, i) / 32768.0f;
                            i += 2;
                        }

                        if (bufferIndex >= buffer.Length || i >= e.BytesRecorded)
                        {
                            processor.Process(buffer.AsSpan(0, bufferIndex));
                            bufferIndex = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;

            waveIn.StopRecording();
            lock (outputSync)
                processor.Finish();
            waveIn.Dispose();
        }
    }
}