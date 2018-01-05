using System.Drawing;
using System.IO;
using Therezin.GeoJsonRenderer;

namespace GeoJsonConsole
{
	class Program
	{
		static void Main(string[] args)
		{
			var reader = new StreamReader("testdata1.json");
			var Json = reader.ReadToEnd();
			var R = new GeoJsonRenderer();			
			R.DrawingFeature += R_DrawingFeature;
			R.LoadGeoJson(Json);
			R.FitLayersToPage(640, 480);
			R.SaveImage(@"D:\TEMP\example4.png", 640, 480);
			R.Dispose();			
		}

		private static void R_DrawingFeature(object sender, DrawingFeatureEventArgs e)
		{
			var OptionalStyle = new DrawingStyle(new Pen(Color.Blue, 5.0f), new SolidBrush(Color.DarkBlue));
			if (e.Feature.Properties.ContainsKey("FLOOR") && e.Feature.Properties["FLOOR"].ToString() == "1")
			{
				e.Style = OptionalStyle;
			}
		}
	}
}
