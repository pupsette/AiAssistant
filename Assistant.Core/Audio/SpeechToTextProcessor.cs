using Whisper.net.Ggml;
using Whisper.net;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Assistant.Core.Audio
{
    public unsafe class SpeechToTextProcessor : IWaveSamplesProcessor
    {
        private const int whisperSampleRate = 16000;
        private readonly string fileName;
        private readonly Thread worker;
        private readonly GgmlType modelType;
        private WhisperProcessorBuilder? processorBuilder;
        private int sampleRate;
        private readonly object processSync = new();

        // current state
        private readonly object stateSync = new();
        private Exception? stateException;
        private SpeechToTextProcessorState state;

        private DateTime lastTextTime;
        private string lastText;

        public SpeechToTextProcessor(GgmlType modelType)
        {
            fileName = $"ggml-{modelType.ToString().ToLowerInvariant()}.bin";
            this.modelType = modelType;

            worker = new Thread(Initialization);
            worker.IsBackground = true;
            worker.Start();
        }

        public OnSegmentEventHandler? OnSegmentEvent;

        public SpeechToTextProcessorState GetState(out Exception? ex)
        {
            lock (stateSync)
            {
                ex = stateException;
                return state;
            }
        }

        private void Initialization()
        {
            try
            {
                // Download model, if needed.
                EnsureModelFile();

                WhisperFactory whisperFactory = WhisperFactory.FromPath(fileName);
                processorBuilder = whisperFactory.CreateBuilder()
                                .WithLanguage("de")
                                .WithTemperature(0.0f)
                                .WithProbabilities()
                                .WithSegmentEventHandler(e =>
                                {
                                    lastText = e.Text;
                                    lastTextTime = DateTime.Now;
                                    OnSegmentEvent?.Invoke(e);
                                });

                UpdateState(SpeechToTextProcessorState.Ready);
            }
            catch (Exception ex)
            {
                UpdateState(SpeechToTextProcessorState.Error, ex);
                return;
            }
        }

        public void WaitForReady()
        {
            lock (stateSync)
            {
                while (state == SpeechToTextProcessorState.Initializing)
                    Monitor.Wait(stateSync);

                if (state == SpeechToTextProcessorState.Error)
                    throw new Exception($"Processor failed to initialize: {stateException?.Message}", stateException);
            }
        }

        private void UpdateState(SpeechToTextProcessorState newState, Exception? ex = null)
        {
            lock (stateSync)
            {
                state = newState;
                stateException = ex;
                Monitor.PulseAll(stateSync);
            }
        }

        private void EnsureModelFile()
        {
            if (File.Exists(fileName))
                return;

            using var modelStream = WhisperGgmlDownloader.GetGgmlModelAsync(modelType).ConfigureAwait(false).GetAwaiter().GetResult();
            using var fileWriter = File.OpenWrite(fileName);
            modelStream.CopyTo(fileWriter);
        }

        public void Process(ReadOnlySpan<float> samples)
        {
            if (state != SpeechToTextProcessorState.Ready)
                return;

            using WhisperProcessor processor = processorBuilder!.WithPrompt(DateTime.Now - lastTextTime < TimeSpan.FromSeconds(2) ? lastText ?? "" : "").Build();
            processor.Process(samples);
        }

        public void Process(byte[] wavFile)
        {
            if (state != SpeechToTextProcessorState.Ready)
                return;

            using WhisperProcessor processor = processorBuilder!.Build();
            processor.Process(new MemoryStream(wavFile));
        }

        public void Setup(WaveFormat waveFormat)
        {
            if (waveFormat == null)
                throw new ArgumentNullException(nameof(waveFormat));

            if (waveFormat.Channels != 1)
                throw new NotSupportedException("Only mono audio is supported.");

            sampleRate = waveFormat.SampleRate;
        }

        public void Finish()
        {
        }
    }
}