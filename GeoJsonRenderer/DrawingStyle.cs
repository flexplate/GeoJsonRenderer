using System.Drawing;

namespace Therezin.GeoJsonRenderer
{
	public class DrawingStyle
	{
		public Pen LinePen { get; set; }
		public Brush FillBrush { get; set; }

		public DrawingStyle(Pen linePen, Brush fillBrush)
		{
			LinePen = linePen;
			FillBrush = fillBrush;
		}
	}
}