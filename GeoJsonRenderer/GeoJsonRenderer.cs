using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using GeoJSON.Net;
using Newtonsoft.Json;
using GeoJSON.Net.Geometry;
using GeoJSON.Net.Feature;

namespace Therezin.GeoJsonRenderer
{
    public class GeoJsonRenderer
    {
        private Bitmap Bitmap;
        private Graphics Graphics;

        public bool RenderGeoJson(string json, string outputPath, int width, int height)
        {
            var GeoJsonObjects = JsonConvert.DeserializeObject<FeatureCollection>(json);

            // Find envelope of geometry
            Envelope Extents = Envelope.FindExtents(GeoJsonObjects);

            // Rebase to zero
            RebaseGeometry(GeoJsonObjects, Extents);

            // Zoom to extents

            // Create canvas
            Bitmap = new Bitmap(width, height);
            Graphics = Graphics.FromImage(Bitmap);
            Graphics.FillRectangle(Brushes.White, new Rectangle(0, 0, width, height));

            // Draw [TEST, REDO.]
            foreach (var Item in GeoJsonObjects.Features)
            {
                DrawGeometry(Item.Geometry);
            }

            // Save
            Bitmap.Save(outputPath);
            return true;
        }

        /// <summary>
        /// Iterate through feature collection and rebase its coordinates around zero.
        /// </summary>
        /// <param name="features"></param>
        /// <param name="envelope"></param>
        private void RebaseGeometry(FeatureCollection features, Envelope envelope)
        {
            foreach (Feature Feature in features.Features)
            {
                
            }
        }

        /// <summary>
        /// Recursively draw a geometry object.
        /// </summary>
        private void DrawGeometry(IGeometryObject geometry)
        {
            switch (geometry.Type)
            {
                case GeoJSONObjectType.Point:
                    var Position = ((GeoJSON.Net.Geometry.Point)geometry).Coordinates;
                    PointF PtF = new PointF() { X = (float)Position.Latitude, Y = (float)Position.Longitude };
                    Graphics.DrawLine(StyleChooser(), PtF, PtF);
                    break;
                case GeoJSONObjectType.MultiPoint:
                    break;
                case GeoJSONObjectType.LineString:
                    Graphics.DrawLines(StyleChooser(), ConvertPositionsToPoints(((LineString)geometry).Coordinates));
                    break;
                case GeoJSONObjectType.MultiLineString:
                    foreach (var Line in ((MultiLineString)geometry).Coordinates)
                    {
                        DrawGeometry(Line);
                    }
                    break;
                case GeoJSONObjectType.Polygon:
                    foreach (var PolyLine in ((Polygon)geometry).Coordinates)
                    {
                        Graphics.DrawPolygon(StyleChooser(), ConvertPositionsToPoints(PolyLine.Coordinates));
                    }
                    break;
                case GeoJSONObjectType.MultiPolygon:
                    break;
                case GeoJSONObjectType.GeometryCollection:
                    foreach (var Geo in ((GeometryCollection)geometry).Geometries)
                    {
                        DrawGeometry(Geo);
                    }
                    break;
                case GeoJSONObjectType.Feature:
                    break;
                case GeoJSONObjectType.FeatureCollection:
                    break;
                default:
                    break;
            }

        }
                
                private PointF[] ConvertPositionsToPoints(List<IPosition> positions)
        {
            var OutList = new List<PointF>();
            for (int i = 0; i < positions.Count; i++)
            {
                OutList.Add(new PointF() { X = (int)positions[i].Latitude, Y = (int)positions[i].Longitude });
            }
            return OutList.ToArray();
        }


        public static void TestCreateFile(string fullPath)
        {
            using (var Bmp = new Bitmap(400, 300))
            {
                Graphics G = Graphics.FromImage(Bmp);
                G.FillRectangle(Brushes.White, new Rectangle(0, 0, 400, 300));
                Pen GreenPen = new Pen(new SolidBrush(Color.Green), 2);
                G.DrawLine(GreenPen, new System.Drawing.Point(30, 30), new System.Drawing.Point(300, 160));
                Bmp.Save(fullPath);
            }
        }

        private Pen StyleChooser()
        {
            return new Pen(new SolidBrush(Color.Green), 2);
        }

    }
}
