using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Therezin.GeoJsonRenderer;


namespace GeoJsonConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var R = new GeoJsonRenderer();
            var reader = new StreamReader("testdata1.json");
            var Json = reader.ReadToEnd();

            R.RenderGeoJson(Json, @"D:\TEMP\test.png", 400, 300);
		}
    }
}
 