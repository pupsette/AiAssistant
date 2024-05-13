using NAudio.Wave;

namespace Assistant.Core.Audio
{
    public class AudioSilenceDetector : IWaveSamplesProcessor
    {
        private readonly double silenceThreshold;
        private readonly TimeSpan minSilenceDuration;
        private DateTime lastSoundTime;

        public AudioSilenceDetector(TimeSpan minSilenceDuration, double silenceThresholdDB = 35)
        {
            silenceThreshold = silenceThresholdDB;
            this.minSilenceDuration = minSilenceDuration;
            lastSoundTime = DateTime.Now;
        }

        public event Action? OnSilenceEnter;

        public event Action? OnSilenceExit;

        public bool IsSilence { get; private set; }

        public void Process(ReadOnlySpan<float> samples)
        {
            // Calculate average loudness level (RMS)
            double rms = CalculateRMSLevel(samples);

            // Convert RMS level to dB
            double dB = 20 * Math.Log10(rms);

            // Check if dB level is below the silence threshold
            if (dB < silenceThreshold)
            {
                // Check if the minimum silence duration is reached
                if (!IsSilence && IsSilenceDurationReached())
                {
                    IsSilence = true;
                    OnSilenceEnter?.Invoke();
                }
            }
            else
            {
                if (IsSilence)
                {
                    IsSilence = false;
                    OnSilenceExit?.Invoke();
                }
                // Reset the silence duration timer
                lastSoundTime = DateTime.Now;
            }
        }

        private bool IsSilenceDurationReached()
        {
            TimeSpan timeSinceLastSound = DateTime.Now - lastSoundTime;
            return timeSinceLastSound >= minSilenceDuration;
        }

        private double CalculateRMSLevel(ReadOnlySpan<float> samples)
        {
            double sum = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double sample = samples[i] * short.MaxValue;
                sum += sample * sample;
            }
            return Math.Sqrt(sum / samples.Length);
        }

        public void Setup(WaveFormat waveFormat)
        {
            if (waveFormat.Channels != 1)
                throw new NotSupportedException("Only mono audio is supported.");
        }

        public void Finish()
        {
        }
    }
}