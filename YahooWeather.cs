using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Wox.Plugin.Weather
{
    public static class YahooWeather
    {
        [Serializable] private class YQLResult<T>
        {
            public YQLQuery<T> query;
            public YQLError error;
        }
        [Serializable] private class YQLError
        {
            public string lang;
            public string description;
        }
        [Serializable] private class YQLQuery<T>
        {
            public int count;
            public string created;
            public string lang;
            public T results;
        }
        [Serializable] private class YQLException : Exception
        {
            public string Query;
            public YQLException() { }
            public YQLException(string message) : base(message) { }
            public YQLException(string message, Exception inner) : base(message, inner) { }
            public YQLException(string message, string query) : base(message) { Query = query; }
            public YQLException(string message, Exception inner, string query) : base(message, inner) { Query = query; }
            private YQLException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context)
                : base(info, context) { }
        }
        private static string YQLEscape(string str)
        {
            return str.Replace(@"\", @"\\").Replace("\"", "\\\"");
        }
        private static T YQLExec<T>(string yql)
        {
            var response = HttpRequest.CreateGetHttpResponse("http://query.yahooapis.com/v1/public/yql?format=json&q=" + System.Uri.EscapeUriString(yql), null, null, null);
            var s = response.GetResponseStream();
            if (s != null)
            {
                var json = new System.IO.StreamReader(s).ReadToEnd();
                var result = JsonConvert.DeserializeObject<YQLResult<T>>(json);
                if (result.error != null)
                    throw new YQLException(result.error.description, yql);

                if (result.query == null)
                    throw new YQLException("Query returns null", yql);

                return result.query.results;
            }

            throw new System.Net.WebException("Failed to query YQL: " + response.StatusDescription, null, System.Net.WebExceptionStatus.ProtocolError, response);
        }
        private static Object YQLExec<T1, T2>(string yql)
        {
            var response = HttpRequest.CreateGetHttpResponse("http://query.yahooapis.com/v1/public/yql?format=json&q=" + System.Uri.EscapeUriString(yql), null, null, null);
            var s = response.GetResponseStream();
            if (s != null)
            {
                var json = new System.IO.StreamReader(s).ReadToEnd();
                try
                {
                    var result = JsonConvert.DeserializeObject<YQLResult<T1>>(json);
                    if (result.error != null)
                        throw new YQLException(result.error.description, yql);

                    if (result.query == null)
                        throw new YQLException("Query returns null", yql);

                    return result.query.results;
                }
                catch (JsonSerializationException)
                {
                    var result = JsonConvert.DeserializeObject<YQLResult<T2>>(json);
                    if (result.error != null)
                        throw new YQLException(result.error.description, yql);

                    if (result.query == null)
                        throw new YQLException("Query returns null", yql);

                    return result.query.results;
                }
            }

            throw new System.Net.WebException("Failed to query YQL: " + response.StatusDescription, null, System.Net.WebExceptionStatus.ProtocolError, response);
        }

        [Serializable] public class PlaceResult
        {
            public Place Result;
        }
        [Serializable] public class PlaceResults
        {
            public List<Place> Result;
        }
        [Serializable] public class Place
        {
            public String quality;
            public String latitude;
            public String longitude;
            public String offsetlat;
            public String offsetlon;
            public String radius;
            public String name;
            public String line1;
            public String line2;
            public String line3;
            public String line4;
            public String house;
            public String street;
            public String xstreet;
            public String unittype;
            public String unit;
            public String postal;
            public String neighborhood;
            public String city;
            public String county;
            public String state;
            public String country;
            public String countrycode;
            public String statecode;
            public String countycode;
            public String uzip;
            public String hash;
            public String woeid;
            public String woetype;

            public override string ToString()
            {
                return String.Join(", ", new string[] { line1, line2, line3, line4}.Where(o => !string.IsNullOrEmpty(o)).ToArray());
            }
        }
        public static List<Place> QueryPlace(string query)
        {
            var yql = String.Format("select * from geo.placefinder where text=\"{0}\"", YQLEscape(query));
            var result = YQLExec<PlaceResult, PlaceResults>(yql);

            if (result is PlaceResult)
                if (((PlaceResult)result).Result != null)
                    return new List<Place>() { ((PlaceResult)result).Result };

            if (result is PlaceResults)
                if (((PlaceResults)result).Result != null)
                    return ((PlaceResults)result).Result.ToList();

            return null;
        }

        [Serializable] public class WeatherResult
        {
            public Weather channel;
        }
        [Serializable] public class Weather
        {
            public string title;
            public string link;
            public string description;
            public string language;
            public string lastBuildDate;
            public string ttl;
            public Location location;
            public Units units;
            public Wind wind;
            public Atmosphere atmosphere;
            public Astronomy astronomy;
            public Item item;
            public Place place;

            [Serializable] public class Location
            {
                public string city;
                public string country;
                public string region;
            }
            [Serializable] public class Units
            {
                public string distance;
                public string pressure;
                public string speed;
                public string temperature;
            }
            [Serializable] public class Wind
            {
                public string chill;
                public string direction;
                public string speed;
            }
            [Serializable] public class Atmosphere
            {
                public string humidity;
                public string pressure;
                public string rising;
                public string visibility;
            }
            [Serializable] public class Astronomy
            {
                public string sunrise;
                public string sunset;
            }
            [Serializable] public class Item
            {
                public string title;
                [JsonProperty("lat")] public string latitute;
                [JsonProperty("long")] public string longitute;
                public string link;
                public string pubData;
                public Condition condition;
                public string description;
                public List<Forecast> forecast;

                [Serializable] public class Condition
                {
                    public string code;
                    public string date;
                    public string temp;
                    public string text;
                }
                [Serializable] public class Forecast
                {
                    public string code;
                    public string date;
                    public string day;
                    public string high;
                    public string low;
                    public string text;
                }
            }
        }
        [Serializable] public enum TemperatureUnit
        {
            Centigrade,
            Fahrenheit
        }
        public static Weather QueryWeather(string woeid, TemperatureUnit unit)
        {
            var yql = String.Format("select * from weather.forecast where woeid=\"{0}\" and u = \"{1}\"", YQLEscape(woeid), unit == TemperatureUnit.Centigrade ? "c" : "f");
            var result = YQLExec<WeatherResult>(yql);
            return result != null ? result.channel : null;
        }
        public static Weather QueryWeather(Place place, TemperatureUnit unit)
        {
            var result = QueryWeather(place.woetype, unit);
            if (result != null)
            {
                result.place = place;
                return result;
            }
            return null;
        }
    }
}
