using System.IO;
using Therezin.GeoJsonRenderer;

namespace GeoJsonConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] Filenames = { "testdata3.json" };
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
                R.DefaultStyle = new DrawingStyle(new System.Drawing.Pen(System.Drawing.Color.Green, 1f), System.Drawing.Brushes.DarkBlue);
                R.CropFeatures(4000,1000,5000,4000);
                R.PageHeight = 200;
                R.PageWidth = 200;
                //R.FitLayersToPage(300, 300);
                R.SaveImage(@"D:\TEMP\example7.png");
            }


        }

    }
}
