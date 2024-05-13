using NAudio.Wave;

namespace Assistant.Core.Audio
{
    public static class WaveFormatExtensions
    {
        public static int ToSampleCount(this WaveFormat format, TimeSpan timeSpan)
        {
            return Math.Max(0, (int)(timeSpan.TotalMilliseconds * format.SampleRate / 1000));
        }

        public static int ToSampleCount(this WaveFormat format, int timeMs)
        {
            return Math.Max(0, (int)((long)timeMs * format.SampleRate / 1000));
        }
    }
}