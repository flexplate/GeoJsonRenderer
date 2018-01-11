using System.IO;
using Therezin.GeoJsonRenderer;

namespace GeoJsonConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var reader = new StreamReader("testdata2.json");
            var Json = reader.ReadToEnd();
            using (var R = new GeoJsonRenderer())
            {
                R.LoadGeoJson(Json);
                R.Paginate(200, 100, 0.5, 20);
                R.SaveImage(@"D:\TEMP\example5");
            }

        }

    }
}
