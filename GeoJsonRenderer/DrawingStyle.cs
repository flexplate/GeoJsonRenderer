using System.Drawing;

namespace Therezin.GeoJsonRenderer
{
	/// <summary>
	/// Provides styling for GeoJsonRenderer to draw features.
	/// </summary>
	public class DrawingStyle
	{
		/// <summary>
		/// Styling applied to Point, LineString, and the outer edge of Polygon objects.
		/// </summary>
		public Pen LinePen { get; set; }

		/// <summary>
		/// Styling applied to the interior of Polygon objects.
		/// </summary>
		public Brush FillBrush { get; set; }

		/// <summary>
		/// Initialise a new DrawingStyle with outline and fill styles.
		/// </summary>
		/// <param name="linePen">Styling applied to Point, LineString, and the outer edge of Polygon objects.</param>
		/// <param name="fillBrush">Styling applied to the interior of Polygon objects.</param>
		public DrawingStyle(Pen linePen, Brush fillBrush)
		{
			LinePen = linePen;
			FillBrush = fillBrush;
		}
	}
}