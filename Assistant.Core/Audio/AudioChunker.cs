using NAudio.Wave;

namespace Assistant.Core.Audio
{
    public class AudioChunker : IWaveSamplesProcessor
    {
        private readonly double silenceThreshold;
        private readonly int resolutionMs;
        private readonly IWaveSamplesProcessor processor;
        private readonly TimeSpan minSilenceDuration;
        private readonly TimeSpan minChunkDuration;
        private readonly TimeSpan desiredChunkDuration;

        private float[]? sampleBuffer;
        private double[]? db;
        private int minBlocks;
        private int desiredBlocks;
        private int minSilenceBlocks;
        private int samplesPerBlock;

        private int currentNumberOfProcessedBlocks;
        private int currentNumberOfSamples;
        private bool isSilent = true;

        public AudioChunker(IWaveSamplesProcessor processor, TimeSpan minSilenceDuration, TimeSpan minChunkDuration, TimeSpan desiredChunkDuration, double silenceThresholdDB = 30, int resolutionMs = 100)
        {
            silenceThreshold = silenceThresholdDB;
            this.resolutionMs = resolutionMs;
            this.processor = processor;
            this.minSilenceDuration = minSilenceDuration;
            this.minChunkDuration = minChunkDuration;
            this.desiredChunkDuration = desiredChunkDuration;
        }

        private ReadOnlySpan<float> TrimStart(ReadOnlySpan<float> samples)
        {
            for (int i = 0; i < samples.Length; i += samplesPerBlock)
            {
                ReadOnlySpan<float> tmp = samples.Slice(i);
                if (tmp.Length > samplesPerBlock)
                    tmp = tmp.Slice(0, samplesPerBlock);
                if (CalculateDb(tmp) >= silenceThreshold)
                    return samples.Slice(i);
            }
            return new ReadOnlySpan<float>();
        }

        private bool IsEndSilent()
        {
            if (currentNumberOfProcessedBlocks <= minSilenceBlocks)
                return false;

            for (int i = 1; i <= minSilenceBlocks; i++)
            {
                if (db[currentNumberOfProcessedBlocks - i] >= silenceThreshold)
                    return false;
            }

            return true;
        }

        private int FindSilence()
        {
            int minDB = currentNumberOfProcessedBlocks;
            double minDBAvgValue = double.MaxValue;
            for (int i = currentNumberOfProcessedBlocks - 1; i >= minBlocks; i--)
            {
                double sum = db[i];
                sum += i > 0 ? db[i - 1] : silenceThreshold;
                sum += i + 1 < currentNumberOfProcessedBlocks ? db[i + 1] : silenceThreshold;
                double avg = sum / 3;
                if (avg < minDBAvgValue)
                {
                    minDBAvgValue = avg;
                    minDB = i;
                }
            }

            return minDB;
        }

        private void SendChunkToProcessor(int blockCount)
        {
            int sampleCount = blockCount * samplesPerBlock;
            if (sampleCount > currentNumberOfSamples)
                throw new ArgumentException($"Trying to send {sampleCount} samples, while the buffer holds only {currentNumberOfSamples}");

            processor.Process(sampleBuffer.AsSpan(0, sampleCount));

            if (currentNumberOfProcessedBlocks > blockCount)
                Array.Copy(db, blockCount, db, 0, currentNumberOfProcessedBlocks - blockCount);
            if (currentNumberOfSamples > sampleCount)
                Array.Copy(sampleBuffer, sampleCount, sampleBuffer, 0, currentNumberOfSamples - sampleCount);

            currentNumberOfProcessedBlocks -= blockCount;
            currentNumberOfSamples -= sampleCount;

            isSilent = true;
        }

        public void Process(ReadOnlySpan<float> samples)
        {
            if (isSilent)
            {
                samples = TrimStart(samples);
                if (samples.Length > 0)
                    isSilent = false;
                else
                    return;
            }

            int remainingSpaceInBuffer = sampleBuffer!.Length - currentNumberOfSamples;
            if (remainingSpaceInBuffer < samples.Length)
                Array.Resize(ref sampleBuffer, sampleBuffer.Length * 2 + samples.Length);
            samples.CopyTo(sampleBuffer.AsSpan(currentNumberOfSamples));
            currentNumberOfSamples += samples.Length;

            while ((currentNumberOfProcessedBlocks + 1) * samplesPerBlock <= currentNumberOfSamples)
            {
                db[currentNumberOfProcessedBlocks] = CalculateDb(sampleBuffer.AsSpan(currentNumberOfProcessedBlocks * samplesPerBlock, samplesPerBlock));
                currentNumberOfProcessedBlocks++;
            }

            if (currentNumberOfProcessedBlocks > minBlocks && IsEndSilent())
            {
                SendChunkToProcessor(currentNumberOfProcessedBlocks);
            }
            else if (currentNumberOfProcessedBlocks >= desiredBlocks)
            {
                SendChunkToProcessor(FindSilence());
            }
        }

        private static double CalculateDb(ReadOnlySpan<float> samples)
        {
            // Calculate average loudness level (RMS)
            double sum = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double sample = samples[i] * short.MaxValue;
                sum += sample * sample;
            }
            double rms = Math.Sqrt(sum / samples.Length);

            // Convert RMS level to dB
            double dB = 20 * Math.Log10(rms);
            return dB;
        }

        public void Setup(WaveFormat waveFormat)
        {
            processor.Setup(waveFormat);

            if (waveFormat.Channels != 1)
                throw new NotSupportedException("Only mono audio is supported.");

            samplesPerBlock = waveFormat.ToSampleCount(resolutionMs);
            minSilenceBlocks = (int)(waveFormat.ToSampleCount(minSilenceDuration) / (double)samplesPerBlock + 0.5);
            desiredBlocks = (int)(waveFormat.ToSampleCount(desiredChunkDuration) / (double)samplesPerBlock + 0.5);
            minBlocks = (int)(waveFormat.ToSampleCount(minChunkDuration) / (double)samplesPerBlock + 0.5);
            sampleBuffer = new float[desiredBlocks * samplesPerBlock * 2];
            db = new double[desiredBlocks * 5];
        }

        public void Finish()
        {
            processor.Finish();
        }
    }
}