using NAudio.Wave;

namespace Assistant.Core.Audio
{
    public interface IWaveSamplesProcessor
    {
        void Setup(WaveFormat waveFormat);
        void Process(ReadOnlySpan<float> samples);
        void Finish();
    }
}