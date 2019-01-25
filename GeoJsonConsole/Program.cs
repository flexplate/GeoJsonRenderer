using System.IO;
using Therezin.GeoJsonRenderer;

namespace GeoJsonConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] Filenames = { @"D:\TEMP\test.json" };
            string[] Jsons = new string[Filenames.Length];
            for (int i = 0; i < Filenames.Length; i++)
            {
                var Reader = new StreamReader(Filenames[i]);
                string Text = Reader.ReadToEnd();
                Jsons[i] = Text;
            }

            using (var R = new GeoJsonRenderer())
            {
                R.LoadGeoJson(Jsons);
                R.CropFeatures(new Envelope(40, 270, 180, 470), 140, 200);
                R.SaveImage(@"D:\TEMP\test1.png");
            }


            using (var R = new GeoJsonRenderer())
            {
                R.LoadGeoJson(Jsons);
                R.FitLayersToPage(800, 600);
                R.SaveImage(@"D:\TEMP\test2.png");
            }


        }

    }
}
