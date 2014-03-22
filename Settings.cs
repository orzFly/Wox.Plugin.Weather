using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.Weather
{
    [Serializable]
    public class Settings
    {
        public YahooWeather.TemperatureUnit TemperatureUnit;
        public List<YahooWeather.PlaceSuggestion> FavoritePlaces;
    }
}
