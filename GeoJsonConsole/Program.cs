using System.Drawing;
using System.IO;
using Therezin.GeoJsonRenderer;

namespace GeoJsonConsole
{
	class Program
    {
        static void Main(string[] args)
        {
            var R = new GeoJsonRenderer();
			R.OptionalStyle = new DrawingStyle(new Pen(Color.Blue, 5.0f), new SolidBrush(Color.DarkBlue));
            var reader = new StreamReader("testdata1.json");
            var Json = reader.ReadToEnd();

			R.RenderGeoJson(Json, @"D:\TEMP\test.png", 400, 300, null, (f => f.Properties["FLOOR"].ToString() == "G"));
			
		}
    }
}
 