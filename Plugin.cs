using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace Wox.Plugin.Weather
{
    public class Plugin : IPlugin
    {
        protected PluginInitContext context;
        protected OrderedDictionary placeCache;
        protected OrderedDictionary weatherCache;
        public void Init(PluginInitContext context)
        {
            this.context = context;
            this.placeCache = new OrderedDictionary();
            this.weatherCache = new OrderedDictionary();
        }

        void CleanCache()
        {
            Array.ForEach(new OrderedDictionary[] { this.placeCache, this.weatherCache }, o =>
            {
                foreach (var item in o.OfType<KeyValuePair<object, DateTime>>().Select(t => (DateTime.UtcNow - t.Value).TotalMinutes > 10))
                    o.Remove(item);

                while (o.Count > 20)
                    o.RemoveAt(0);
            });
        }

        String GetIcon(String code)
        {
            return GetIcon(code, "gif");
        }

        String GetIcon(String code, String ext)
        {
            string icon;
            icon = System.IO.Path.Combine(context.CurrentPluginMetadata.PluginDirecotry, "Images\\" + code + "." + ext);
            if (!System.IO.File.Exists(icon))
                icon = null;

            return icon;
        }

        public List<Result> Query(Query query)
        {
            CleanCache();

            var qs = query.GetAllRemainingParameter();
            if (!String.IsNullOrEmpty(qs))
            {
                List<YahooWeather.PlaceSuggestion> places = null;
                foreach (var list in placeCache.OfType<KeyValuePair<object, DateTime>>().Select(o => (List<YahooWeather.PlaceSuggestion>)o.Key))
                {
                    foreach (var item in list)
                    {
                        if (item.ToString() == qs)
                        {
                            places = new List<YahooWeather.PlaceSuggestion>() { item };
                            break;
                        }
                    }
                    if (places != null) break;
                }
                if (places == null)
                {
                    if (placeCache.Contains(qs))
                        places = (List<YahooWeather.PlaceSuggestion>)((KeyValuePair<object, DateTime>)placeCache[qs]).Key;
                    else
                    {
                        places = YahooWeather.QueryPlaceSuggestion(qs);
                        placeCache[qs] = new KeyValuePair<object, DateTime>(places, DateTime.UtcNow);
                    }
                }

                if (places != null && places.Count > 0)
                {
                    var weathers = places
                        .Where(place => place.type != "s")
                        .Select(place =>
                        {
                            try
                            {
                                YahooWeather.Weather weather;
                                if (weatherCache.Contains(place.WoeID))
                                    weather = (YahooWeather.Weather)((KeyValuePair<object, DateTime>)weatherCache[place.WoeID]).Key;
                                else
                                {
                                    weather = YahooWeather.QueryWeather(place, YahooWeather.TemperatureUnit.Centigrade);
                                    weatherCache[place.WoeID] = new KeyValuePair<object, DateTime>(weather, DateTime.UtcNow);
                                }
                                return weather;
                            }
                            catch { return null; }
                        })
                        .Where(o => o != null && o.item != null && o.item.condition != null && o.item.forecast != null)
                        .Take(5).ToList();

                    if (weathers.Count > 1)
                    {
                        return weathers.Select(weather =>
                        {
                            string icon = null;
                            string forecast = null;
                            if (weather.item != null && weather.item.forecast != null && weather.item.forecast.Count > 0)
                            {
                                forecast = String.Format("{0}, {1}～{2}°{3}, {4}", weather.item.forecast[0].text, weather.item.forecast[0].low, weather.item.forecast[0].high, weather.units.temperature.ToUpper(), weather.item.forecast[0].date);

                                icon = GetIcon(weather.item.forecast[0].code);
                            }

                            return new Result()
                            {
                                Title = forecast,
                                SubTitle = weather.woe.ToString(),
                                IcoPath = icon,
                                Action = con =>
                                {
                                    context.ChangeQuery(context.CurrentPluginMetadata.ActionKeyword + " " + weather.woe.ToString());
                                    return false;
                                }
                            };
                        })
                        .ToList();
                    }
                    else if (weathers.Count == 1)
                    {
                        var weather = weathers[0];
                        Func<ActionContext, bool> action = o =>
                        {
                            context.HideApp();
                            context.ShellRun(weather.link);
                            return true;
                        };

                        var list = new List<Result>();
                        list.Add(new Result()
                        {
                            Title = weather.woe.ToString(),
                            SubTitle = weather.item.pubDate,
                            IcoPath = GetIcon("favicon", "ico"),
                            Action = action
                        });

                        list.AddRange(weather.item.forecast.Select(day => {
                            return new Result()
                            {
                                Title = String.Format("{0}, {1}～{2}°{3}", day.text, day.low, day.high, weather.units.temperature.ToUpper()),
                                SubTitle = day.date,
                                IcoPath = GetIcon(day.code),
                                Action = action
                            };
                        }));

                        return list;
                    }
                }
            }
            return null;
        }
    }
}
