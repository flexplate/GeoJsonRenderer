using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeoJSON.Net.Feature;
using Newtonsoft.Json;

namespace Therezin.GeoJsonRenderer
{
    /// <summary>
    /// Represents a GeoJSON document containing additional properties and a GeoJSON FeatureCollection under a node called "GeoJSON".
    /// </summary>
    public class Layer
    {
        /// <summary>
        /// Instantiate a blank layer.
        /// </summary>
        public Layer()
        {
        }

        /// <summary>
        /// Instantiate a layer from a collection of Features.
        /// </summary>
        /// <param name="features">Features to populate layer with.</param>
        public Layer(IEnumerable<Feature> features)
        {
            Features = features.ToList();
        }

        /// <summary>
        /// Instantiate a layer from a FeatureCollection.
        /// </summary>
        /// <param name="featureCollection">Features to populate layer with.</param>
        public Layer(FeatureCollection featureCollection)
        {
            Features = featureCollection.Features;
        }

        /// <summary>
        /// GeoJSON feature collection.
        /// </summary>
        [JsonProperty(PropertyName = "features", Required = Required.Always)]
        public List<Feature> Features { get; set; }

        /// <summary>
        /// Extended properties of GeoJSON object.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }

        /// <summary>
        /// Treat this layer as a FeatureCollection.
        /// </summary>
        public static implicit operator FeatureCollection(Layer layer)
        {
            return new FeatureCollection(layer.Features);
        }

        /// <summary>
        /// Treat a FeatureCollection as a Layer.
        /// </summary>
        public static implicit operator Layer(FeatureCollection features)
        {
            return new Layer(features);
        }
    }
}
