using System.IO;
using Therezin.GeoJsonRenderer;

namespace GeoJsonConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] Filenames = { "test-80.json" };
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
                var LayerSize = Envelope.FindExtents(R.Layers[0]);
                R.Layers.Add((Layer)R.Layers[0].Clone());
                R.RotateAndScaleLayer(R.Layers[1], (int)LayerSize.Width, (int)LayerSize.Height, rotate:true);
                R.FitLayersToPage(800, 600);
                R.SaveImage(@"D:\TEMP\test2.png");
            }

            /*
            using (var R = new GeoJsonRenderer())
            {
                R.LoadGeoJson(Jsons);
                R.CropFeatures(400000, 150000, 420000, 165000);
                R.PageHeight = 1200;
                R.PageWidth = 1200;
                //R.FitLayersToPage(300, 300);
                R.SaveImage(@"D:\TEMP\test1.png");
            }
            */

        }

    }
}
