using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Wox.Plugin.Weather
{
    public class Plugin : IPlugin
    {
        protected PluginInitContext context;
        protected OrderedDictionary placeCache;
        protected OrderedDictionary weatherCache;
        protected Settings settings;
        protected string settingsPath;

        public void Init(PluginInitContext context)
        {
            this.context = context;
            this.placeCache = new OrderedDictionary();
            this.weatherCache = new OrderedDictionary();

            LoadSettings();
        }

        void LoadSettings()
        {
            this.settingsPath = System.IO.Path.Combine(context.CurrentPluginMetadata.PluginDirecotry, "settings.json");
            try
            {
                this.settings = JsonConvert.DeserializeObject<Settings>(System.IO.File.ReadAllText(this.settingsPath));
            }catch{
                this.settings = new Settings();
            }

            if (this.settings.TemperatureUnit == null)
                this.settings.TemperatureUnit = YahooWeather.TemperatureUnit.Centigrade;

            if (this.settings.FavoritePlaces == null)
                this.settings.FavoritePlaces = new List<YahooWeather.PlaceSuggestion>();
        }

        void SaveSettings()
        {
            try
            {
                System.IO.File.WriteAllText(this.settingsPath, JsonConvert.SerializeObject(this.settings, Formatting.Indented));
            }
            catch {}
        }

        void CleanCache(OrderedDictionary o)
        {
            foreach (var item in o.OfType<KeyValuePair<object, DateTime>>().Select(t => (DateTime.UtcNow - t.Value).TotalMinutes > 10))
                o.Remove(item);

            while (o.Count > 20)
                o.RemoveAt(0);
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
            CleanCache(weatherCache);

            var qs = query.GetAllRemainingParameter();
            List<YahooWeather.PlaceSuggestion> places = null;
            if (!String.IsNullOrEmpty(qs))
            {
                foreach (var list in placeCache.Values.OfType<KeyValuePair<object, DateTime>>().Select(o => (List<YahooWeather.PlaceSuggestion>)o.Key))
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
                CleanCache(placeCache);
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
            }
            else
            {
                places = this.settings.FavoritePlaces;
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
                                weather = YahooWeather.QueryWeather(place, this.settings.TemperatureUnit);
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
                        Title = weather.woe.ToString() + " - Yahoo! Weather",
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

                    if (this.settings.FavoritePlaces.Count(o => o.woeid == weather.woe.WoeID) > 0)
                    {
                        list.Add(new Result()
                        {
                            Title = "Remove from My Places",
                            IcoPath = GetIcon("remove", "png"),
                            Action = con =>
                            {
                                this.settings.FavoritePlaces.RemoveAll(o => o.woeid == weather.woe.WoeID);
                                SaveSettings();
                                context.ChangeQuery(context.CurrentPluginMetadata.ActionKeyword + " ");
                                return false;
                            }
                        });
                    }
                    else
                    {
                        list.Add(new Result()
                        {
                            Title = "Add to My Places",
                            IcoPath = GetIcon("add", "png"),
                            Action = con =>
                            {
                                this.settings.FavoritePlaces.Add((YahooWeather.PlaceSuggestion)weather.woe);
                                SaveSettings();
                                context.ChangeQuery(context.CurrentPluginMetadata.ActionKeyword + " ");
                                return false;
                            }
                        });
                    }
                        
                    return list;
                }
            }
            return new List<Result>();
        }
    }
}
