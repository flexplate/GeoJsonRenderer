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
			R.OptionalStyle = new DrawingStyle(new Pen(Color.Blue, 5.0f), new SolidBrush(Color.DarkBlue));
			R.AlternativeStyleFunction = (f => f.Properties.ContainsKey("FLOOR") && f.Properties["FLOOR"].ToString() == "1");
			R.LoadGeoJson(Json);
			R.FitLayersToPage(640, 480);
			R.SaveImage(@"D:\TEMP\example1.png", 640, 480);
			R.Dispose();

			R = new GeoJsonRenderer();
			R.OptionalStyle = new DrawingStyle(new Pen(Color.Blue, 5.0f), new SolidBrush(Color.DarkBlue));
			R.AlternativeStyleFunction = (f => f.Properties.ContainsKey("FLOOR") && f.Properties["FLOOR"].ToString() == "1");
			R.LoadGeoJson(Json);
			R.FitLayersToPage(640, 480, 20);
			R.SaveImage(@"D:\TEMP\example5.png", 640, 480);

		}
	}
}
