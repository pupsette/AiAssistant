using NAudio.Wave;

namespace Assistant.Core.Audio
{
    public class WaveFileWriter : IWaveSamplesProcessor
    {
        private readonly string? outputFile;
        private readonly string? filePrefix;
        private readonly string? fileSuffix;
        private int fileCounter = 1;
        private readonly bool oneFilePerInvocation;
        private readonly Stream? stream;
        private NAudio.Wave.WaveFileWriter? internalWriter;
        private WaveFormat? waveFormat;

        public WaveFileWriter(string outputFile, bool oneFilePerInvocation = false)
        {
            this.outputFile = outputFile;
            this.oneFilePerInvocation = oneFilePerInvocation;

            if (oneFilePerInvocation)
            {
                filePrefix = Path.GetFileNameWithoutExtension(outputFile) + ".";
                fileSuffix = Path.GetExtension(outputFile);
            }
        }

        public WaveFileWriter(Stream stream)
        {
            this.stream = stream;
        }

        public void Process(ReadOnlySpan<float> samples)
        {
            if (oneFilePerInvocation)
                internalWriter = new NAudio.Wave.WaveFileWriter($"{filePrefix}{fileCounter++}{fileSuffix}", waveFormat);

            for (int i = 0; i < samples.Length; i++)
                internalWriter!.WriteSample(samples[i]);

            if (oneFilePerInvocation)
            {
                internalWriter!.Flush();
                internalWriter.Close();
                internalWriter = null;
            }
        }

        public void Setup(WaveFormat waveFormat)
        {
            this.waveFormat = waveFormat;
            if (oneFilePerInvocation)
                return;

            if (outputFile != null)
                internalWriter = new NAudio.Wave.WaveFileWriter(outputFile, waveFormat);
            else
                internalWriter = new NAudio.Wave.WaveFileWriter(stream, waveFormat);
        }

        public void Finish()
        {
            internalWriter?.Flush();
            internalWriter = null;
        }
    }
}