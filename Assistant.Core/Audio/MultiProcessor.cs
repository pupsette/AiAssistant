using NAudio.Wave;

namespace Assistant.Core.Audio
{
    public class MultiProcessor : IWaveSamplesProcessor
    {
        private readonly IWaveSamplesProcessor[] processors;

        public MultiProcessor(params IWaveSamplesProcessor[] processors)
        {
            this.processors = processors;
        }

        public void Finish()
        {
            foreach (IWaveSamplesProcessor processor in processors)
                processor.Finish();
        }

        public void Process(ReadOnlySpan<float> samples)
        {
            foreach (IWaveSamplesProcessor processor in processors)
                processor.Process(samples);
        }

        public void Setup(WaveFormat waveFormat)
        {
            foreach (IWaveSamplesProcessor processor in processors)
                processor.Setup(waveFormat);
        }
    }
}
