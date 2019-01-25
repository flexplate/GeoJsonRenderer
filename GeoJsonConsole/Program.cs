using System;
using System.IO;
using System.Linq;
using Therezin.GeoJsonRenderer;

namespace GeoJsonConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] Filenames = { @"D:\TEMP\pid-71-1.json", @"D:\TEMP\pid-71-2.json", @"D:\TEMP\pid-71-3.json", @"D:\TEMP\pid-71-4.json" };
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

                for (int i = 0; i < R.Layers.Count; i++)
                {
                    R.Layers[i] = new GeoJSON.Net.Feature.FeatureCollection(R.Layers[i].Features.Where(FilterFeatures()).ToList());
                }

                //R.Paginate(4250, 3000, 0.047, 0.015, 0, 50);
                //R.SaveImage(@"D:\TEMP\GeoJSON Renderer Output Test\" + DateTime.Now.ToFileTimeUtc().ToString() + "-{0}.png");

                R.FitLayersToPage(4250, 3000, 50);
                R.SaveImage(@"D:\TEMP\GeoJSON Renderer Output Test\" + DateTime.Now.ToFileTimeUtc().ToString() + ".png");
            }
        }


        private static Func<GeoJSON.Net.Feature.Feature, bool> FilterFeatures()
        {
            return (
                f => (
                    f.Properties.ContainsKey("FLOOR_LOCATION") &&
                    f.Properties["FLOOR_LOCATION"].ToString() == "G"
                ) || (
                    f.Properties.ContainsKey("FLOOR") &&
                    f.Properties["FLOOR"].ToString() == "G"
                )
            );
        }

    }
}
