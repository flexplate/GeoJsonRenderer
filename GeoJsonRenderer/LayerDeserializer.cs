using GeoJSON.Net.Feature;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Therezin.GeoJsonRenderer
{
    class LayerDeserializer : JsonConverter
    {

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException("Not implemented; use the default.");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var JsonData = JObject.Load(reader);
            if (JsonData["GeoJson"] != null)
            {
                var GeoData = JsonData["GeoJson"].ToObject<FeatureCollection>();

                var Layer = new Layer(GeoData);
                JsonData.Remove("GeoJson");

                IDictionary<string, JToken> Properties = JsonData;
                Layer.Properties = Properties.ToDictionary(p => p.Key, p => (object)p.Value);

                return Layer;
            }
            else
            {
                // No "GeoJSON" member - can we just deserialize entire object?
                return new Layer(JsonData.ToObject<FeatureCollection>());
            }

        }

        public override bool CanRead => true;

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) => true;
    }
}

