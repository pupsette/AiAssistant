using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Assistant.Core.Capabilities
{
    public class TimeFunctions
    {
        [Description("Zeigt das aktuelle Datum und die aktuelle Zeit an.")]
        public string what_time_is_it()
        {
            return DateTime.Now.ToString(CultureInfo.InvariantCulture);
        }

        [Description("Gibt den aktuellen Wochentag zurück.")]
        public string day_of_week_now()
        {
            return DateTime.Now.DayOfWeek.ToString();
        }

        [Description("Gibt den Wochentag eine bestimmten Datums zurück.")]
        public object day_of_week(day_of_week_params input)
        {
            string inp = input.Date;
            if (inp.Length == 2)
                inp = DateTime.Now.ToString("yyyy-MM-") + inp;
            if (inp.Length == 5)
                inp = DateTime.Now.ToString("yyyy-") + inp;
            DateTime dateTime = DateTime.ParseExact(inp, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new { DayOfWeek = dateTime.DayOfWeek.ToString() };
        }

        public class day_of_week_params
        {
            [Description("Ein Datum in einem der folgenden Formate: \"yyyy-MM-dd\" \"MM-dd\" oder \"dd\". \"MM-dd\" bezieht sich auf das aktuelle Jahr, \"dd\" bezieht sich auf den aktuellen Monat. Für dieses Datum soll der Wochentag ermittelt werden.")]
            [Required]
            public string Date { get; set; }
        }
    }
}
