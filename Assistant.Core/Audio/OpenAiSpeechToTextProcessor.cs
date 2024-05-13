using NAudio.Wave;
using OpenAI;

namespace Assistant.Core.Audio
{
    public unsafe class OpenAiSpeechToTextProcessor : IWaveSamplesProcessor
    {
        private static readonly HashSet<string> blacklistHashSet;

        static OpenAiSpeechToTextProcessor()
        {
            // see https://github.com/openai/whisper/discussions/928
            blacklistHashSet = new HashSet<string>(BLACKLIST.Split('\n').Select(s => s.Trim()), StringComparer.InvariantCultureIgnoreCase);
        }

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

                if (blacklistHashSet.Contains(response.Text.Trim()))
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

        private const string BLACKLIST = @"
www.mooji.org
Ondertitels ingediend door de Amara.org gemeenschap
Ondertiteld door de Amara.org gemeenschap
Ondertiteling door de Amara.org gemeenschap
Untertitelung aufgrund der Amara.org-Community
Untertitel im Auftrag des ZDF für funk, 2017
Untertitel von Stephanie Geiges
Untertitel der Amara.org-Community
Untertitel im Auftrag des ZDF, 2017
Untertitel im Auftrag des ZDF, 2020
Untertitel im Auftrag des ZDF, 2018
Untertitel im Auftrag des ZDF, 2021
Untertitelung im Auftrag des ZDF, 2021
Copyright WDR 2021
Copyright WDR 2020
Copyright WDR 2019
SWR 2021
SWR 2020
Sous-titres réalisés para la communauté d'Amara.org
Sous-titres réalisés par la communauté d'Amara.org
Sous-titres fait par Sous-titres par Amara.org
Sous-titres réalisés par les SousTitres d'Amara.org
Sous-titres par Amara.org
Sous-titres par la communauté d'Amara.org
Sous-titres réalisés pour la communauté d'Amara.org
Sous-titres réalisés par la communauté de l'Amara.org
Sous-Titres faits par la communauté d'Amara.org
Sous-titres par l'Amara.org
Sous-titres fait par la communauté d'Amara.org
Sous-titrage ST' 501
Sous-titrage ST'501
Cliquez-vous sur les sous-titres et abonnez-vous à la chaîne d'Amara.org
❤️ par SousTitreur.com
Sottotitoli creati dalla comunità Amara.org
Sottotitoli di Sottotitoli di Amara.org
Sottotitoli e revisione al canale di Amara.org
Sottotitoli e revisione a cura di Amara.org
Sottotitoli e revisione a cura di QTSS
Sottotitoli e revisione a cura di QTSS.
Sottotitoli a cura di QTSS
Subtítulos realizados por la comunidad de Amara.org
Subtitulado por la comunidad de Amara.org
Subtítulos por la comunidad de Amara.org
Subtítulos creados por la comunidad de Amara.org
Subtítulos en español de Amara.org
Subtítulos hechos por la comunidad de Amara.org
Subtitulos por la comunidad de Amara.org
Más información www.alimmenta.com
Subtítulos realizados por la comunidad de Amara.org
Legendas pela comunidade Amara.org
Legendas pela comunidade de Amara.org
Legendas pela comunidade do Amara.org
Legendas pela comunidade das Amara.org
Transcrição e Legendas pela comunidade de Amara.org
Sottotitoli creati dalla comunità Amara.org
Sous-titres réalisés para la communauté d'Amara.org
Sous-titres réalisés para la communauté d'Amara.org
Napisy stworzone przez społeczność Amara.org
Napisy wykonane przez społeczność Amara.org
Zdjęcia i napisy stworzone przez społeczność Amara.org
napisy stworzone przez społeczność Amara.org
Tłumaczenie i napisy stworzone przez społeczność Amara.org
Napisy stworzone przez społeczności Amara.org
Tłumaczenie stworzone przez społeczność Amara.org
Napisy robione przez społeczność Amara.org
www.multi-moto.eu
Редактор субтитров А.Синецкая Корректор А.Егорова
Yorumlarınızıza abone olmayı unutmayın.
Sottotitoli creati dalla comunità Amara.org";

    }
}