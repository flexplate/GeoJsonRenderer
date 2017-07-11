using GeoJSON.Net;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Therezin.GeoJsonRenderer
{
	public class Envelope
	{
		public double MinX { get; set; }
		public double MinY { get; set; }
		public double MaxX { get; set; }
		public double MaxY { get; set; }

		public double Width
		{
			get { return MaxX - MinX; }
		}
		public double Height
		{
			get { return MaxY - MinY; }
		}
		public double AspectRatio
		{
			get { return Width / Height; }
		}


		public Envelope() { }

		public Envelope(double minX, double minY, double maxX, double maxY)
		{
			MinX = minX;
			MinY = minY;
			MaxX = maxX;
			MaxY = maxY;
		}


		public static Envelope FindExtents(List<IGeometryObject> geoJsonObjects, Envelope extents = null)
		{
			var Extents = new Envelope();
			if (extents != null)
			{
				Extents = extents;
			}

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
				else
				{
					switch (Item.Type)
					{
						case GeoJSONObjectType.Point:
							Extents = FindExtents(((Point)Item).Coordinates, Extents);
							break;
						case GeoJSONObjectType.MultiPoint:
							foreach (var Point in ((MultiPoint)Item).Coordinates) { Extents = FindExtents(Point.Coordinates, Extents); }
							break;
						case GeoJSONObjectType.LineString:
							foreach (var Position in ((LineString)Item).Coordinates) { Extents = FindExtents(Position, Extents); }

							break;
						case GeoJSONObjectType.MultiLineString:
							foreach (var Line in ((MultiLineString)Item).Coordinates)
							{
								foreach (var Position in Line.Coordinates) { Extents = FindExtents(Position, Extents); }
							}
							break;
						case GeoJSONObjectType.Polygon:
							foreach (var Line in ((Polygon)Item).Coordinates)
							{
								foreach (var Position in Line.Coordinates) { Extents = FindExtents(Position, Extents); }
							}
							break;
						case GeoJSONObjectType.MultiPolygon:
							foreach (var Poly in ((MultiPolygon)Item).Coordinates)
							{
								foreach (var Line in Poly.Coordinates)
								{
									foreach (var Position in Line.Coordinates) { Extents = FindExtents(Position, Extents); }
								}
							}
							break;
						case GeoJSONObjectType.GeometryCollection:
							Extents = FindExtents(((GeometryCollection)Item).Geometries, Extents);
							break;
						case GeoJSONObjectType.Feature:
							FeatureCollection Collection = new FeatureCollection(new List<Feature>() { (Feature)Item });
							Extents = FindExtents(Collection, Extents);
							break;
						case GeoJSONObjectType.FeatureCollection:
							Extents = FindExtents((FeatureCollection)Item, Extents);
							break;
						default:
							break;
					}
				}
			}
			return Extents;
		}

		public static Envelope FindExtents(FeatureCollection features, Envelope extents = null)
		{
			var Extents = new Envelope();
			if (extents != null)
			{
				Extents = extents;
			}

			var Geometries = new List<IGeometryObject>();
			foreach (var Feature in features.Features)
			{
				Geometries.Add(Feature.Geometry);
			}
			return FindExtents(Geometries, Extents);
		}


		private static Envelope FindExtents(IPosition position, Envelope extents = null)
		{
			var Extents = new Envelope();
			if (extents != null)
			{
				Extents = extents;
			}
			Extents.MinX = new double[] { Extents.MinX, position.Latitude }.Where(v => v != 0).Min();
			Extents.MinY = new double[] { Extents.MinY, position.Longitude }.Where(v => v != 0).Min();
			Extents.MaxX = Math.Max(Extents.MaxX, position.Latitude);
			Extents.MaxY = Math.Max(Extents.MaxY, position.Longitude);
			return Extents;
		}
	}
}