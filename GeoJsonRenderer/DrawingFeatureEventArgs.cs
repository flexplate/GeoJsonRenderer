using GeoJSON.Net.Feature;
using System;
using System.Collections.Generic;

namespace Therezin.GeoJsonRenderer
{
    /// <summary>
    /// <see cref="DrawingFeatureEventArgs"/> is raised immediately before a feature is drawn.
    /// </summary>
	public class DrawingFeatureEventArgs : EventArgs
	{
        /// <summary>
        /// Initialises a new instance of the <see cref="DrawingFeatureEventArgs"/> class.
        /// </summary>
        /// <param name="feature">Feature that is about to be drawn.</param>
        /// <param name="style">Style to apply to <paramref name="feature"/>.</param>
        /// <param name="layerProperties">Foreign members of current feature's layer.</param>
        public DrawingFeatureEventArgs(Feature feature, DrawingStyle style, Dictionary<string, object> layerProperties)
		{
			Feature = feature;
			Style = style;
            LayerProperties = layerProperties;
		}

        /// <summary>
        /// Feature that is about to be drawn.
        /// </summary>
		public Feature Feature { get; set; }

        /// <summary>
        /// Style to apply to feature.
        /// </summary>
		public DrawingStyle Style { get; set; }

        /// <summary>
        /// Foreign members of Layer
        /// </summary>
        public Dictionary<string, object> LayerProperties { get; set; }
    }
}
