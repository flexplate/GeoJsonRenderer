using GeoJSON.Net.Feature;
using System;

namespace Therezin.GeoJsonRenderer
{
	public class DrawingFeatureEventArgs : EventArgs
	{
		public DrawingFeatureEventArgs(Feature feature, DrawingStyle style)
		{
			Feature = feature;
			Style = style;
		}

		public Feature Feature { get; set; }
		public DrawingStyle Style { get; set; }
	}
}
