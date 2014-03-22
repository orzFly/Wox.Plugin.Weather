using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.Weather
{
    public class Plugin : IPlugin
    {
        protected PluginInitContext context;

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        public List<Result> Query(Query query)
        {
            var places = YahooWeather.QueryPlace(query.GetAllRemainingParameter());
            if (places != null && places.Count > 0)
            {
                return places
                    .Select(place =>
                    {
                        try
                        {
                            return YahooWeather.QueryWeather(place, YahooWeather.TemperatureUnit.Centigrade);
                        }
                        catch { return null; }
                    })
                    .Where(o => o != null && o.item != null && o.item.condition != null && o.item.forecast != null)
                    .Take(5)
                    .Select(weather =>
                    {
                        string icon = null;
                        string forecast = null;
                        if (weather.item != null && weather.item.forecast != null && weather.item.forecast.Count > 0)
                        {
                            forecast = String.Format("{0}, {1}～{2}°{3}, {4}", weather.item.forecast[0].text, weather.item.forecast[0].low, weather.item.forecast[0].high, weather.units.temperature.ToUpper(), weather.item.forecast[0].date);

                            icon = System.IO.Path.Combine(context.CurrentPluginMetadata.PluginDirecotry, "Images\\" + weather.item.forecast[0].code + ".gif");
                            if (!System.IO.File.Exists(icon))
                                icon = null;
                        }

                        return new Result()
                        {
                            Title = forecast,
                            SubTitle = weather.place.ToString(),
                            IcoPath = icon
                        };
                    })
                    .ToList();
            }
            return null;
        }
    }
}
