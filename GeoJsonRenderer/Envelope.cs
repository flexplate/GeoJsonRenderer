using GeoJSON.Net;
using System.Collections.Generic;

namespace Therezin.GeoJsonRenderer
{
    public class Envelope
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        public Envelope() { }

        public Envelope(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        private static Envelope FindExtents(List<GeoJSONObject> geoJsonObjects)
        {
            var Extents = new Envelope();
            foreach (GeoJSONObject Item in geoJsonObjects)
            {
                if (Item.BoundingBoxes != null)
                {
                    if (Item.BoundingBoxes.Length == 4)    // -X, -Y, +X, +Y
                    {
                        if (Item.BoundingBoxes[0] < Extents.MinX) { Extents.MinX = Item.BoundingBoxes[0]; }
                        if (Item.BoundingBoxes[1] < Extents.MinY) { Extents.MinY = Item.BoundingBoxes[1]; }
                        if (Item.BoundingBoxes[2] > Extents.MaxX) { Extents.MaxX = Item.BoundingBoxes[2]; }
                        if (Item.BoundingBoxes[3] > Extents.MaxY) { Extents.MaxY = Item.BoundingBoxes[3]; }
                    }
                    else if (Item.BoundingBoxes.Length == 6) // -X, -Y, -Z, +X, +Y, +Z
                    {
                        if (Item.BoundingBoxes[0] < Extents.MinX) { Extents.MinX = Item.BoundingBoxes[0]; }
                        if (Item.BoundingBoxes[1] < Extents.MinY) { Extents.MinY = Item.BoundingBoxes[1]; }
                        if (Item.BoundingBoxes[3] > Extents.MaxX) { Extents.MaxX = Item.BoundingBoxes[3]; }
                        if (Item.BoundingBoxes[3] > Extents.MaxY) { Extents.MaxY = Item.BoundingBoxes[4]; }
                    }
                }
            }
            return Extents;
        }

    }
}