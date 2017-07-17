using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Therezin.GeoJsonRenderer;
using Newtonsoft.Json;
using GeoJSON.Net.Feature;

namespace GeoJsonConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var R = new GeoJsonRenderer();
            var reader = new StreamReader("testdata2.json");
            var Json = reader.ReadToEnd();

			//R.RenderGeoJson(Json, @"D:\TEMP\test.png", 4242, 3000, (f => f.Properties["BUILDING"].ToString() == "1"));

			string[] filenames = { "test-307-areas.json", "test-307-frame.json", "test-307-perimeter.json", "test-307-text.json" };
			List<string> Jsons = new List<string>();
			foreach (var name in filenames)
			{
				Console.Write(name + ": ");
				var Reader = new StreamReader(name);
				string Text = Reader.ReadToEnd();
				Jsons.Add(Text);
				var Features = JsonConvert.DeserializeObject<FeatureCollection>(Text);
				Console.WriteLine(Envelope.FindExtents(Features).ToString());
				
			}
			R.RenderGeoJson(Jsons.ToArray(), @"D:\TEMP\test2.png", 4242, 3000, null, (f => f.Properties.ContainsKey("BUILDING_NO") && f.Properties["BUILDING_NO"].ToString() == "1"));
		}
    }
}
 