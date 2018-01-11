using GeoJSON.Net;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Therezin.GeoJsonRenderer
{
	/// <summary>
	/// Describes a rectangle that encompasses a geographic area, in arbitrary units.
	/// </summary>
	public class Envelope
	{
		/// <summary>
		/// Minimum value of the bounding box's X axis. (i.e., West-most extent)
		/// </summary>
		public double MinX { get; set; }

		/// <summary>
		/// Minimum value of the bounding box's Y axis.
		/// </summary>
		public double MinY { get; set; }

		/// <summary>
		/// Maximum value of the bounding box's X axis.
		/// </summary>
		public double MaxX { get; set; }

		/// <summary>
		/// Maximum value of the bounding box's Y axis.
		/// </summary>
		public double MaxY { get; set; }

		/// <summary>
		/// Width of the envelope (Longitude, in arbitrary units).
		/// </summary>
		public double Width
		{
			get { return MaxX - MinX; }
		}

		/// <summary>
		/// Height of the envelope (Latitude, in arbitrary units).
		/// </summary>
		/// <remarks>Note: not the height in 3-dimensional space. Envelope is a purely 2-dimensional construct.</remarks>
		public double Height
		{
			get { return MaxY - MinY; }
		}

		/// <summary>
		/// The envelope's aspect ratio.
		/// </summary>
		public double AspectRatio
		{
			get { return Width / Height; }
		}

		/// <summary>
		/// Instantiate an empty envelope (Coordinates are [0,0],[0,0]).
		/// </summary>
		public Envelope() { }

		/// <summary>
		/// Instantiate an envelope with the specified minima and maxima.
		/// </summary>
		public Envelope(double minX, double minY, double maxX, double maxY)
		{
			MinX = minX;
			MinY = minY;
			MaxX = maxX;
			MaxY = maxY;
		}

		/// <summary>
		/// Output the envelope's coordinates as a string.
		/// </summary>
		public override string ToString()
		{
			return string.Format("[{0},{1}],[{2},{3}]", MinX, MinY, MaxX, MaxY);
		}

		/// <summary>
		/// Find the smallest bounding box that can contain all FeatureCollections in a given List.
		/// </summary>
		/// <param name="collections">The FeatureCollections whose dimensions we're returning</param>
		/// <returns>An Envelope that can contain all Features in the provided FeatureCollections.</returns>
		public static Envelope FindExtents(List<FeatureCollection> collections)
		{
			var Extents = new Envelope();
			foreach (var Collection in collections)
			{
				var CollectionExtents = FindExtents(Collection);
				// Confusing syntax: Uses LINQ to find smallest, excluding 0. Not actually an array of doubles.
				Extents.MinX = new double[] { Extents.MinX, CollectionExtents.MinX }.Where(v => v != 0).DefaultIfEmpty().Min();
				Extents.MinY = new double[] { Extents.MinY, CollectionExtents.MinY }.Where(v => v != 0).DefaultIfEmpty().Min();
				Extents.MaxX = new double[] { Extents.MaxX, CollectionExtents.MaxX }.Where(v => v != 0).DefaultIfEmpty().Max();
				Extents.MaxY = new double[] { Extents.MaxY, CollectionExtents.MaxY }.Where(v => v != 0).DefaultIfEmpty().Max();
			}
			return Extents;
		}

		/// <summary>
		/// Find the smallest bounding box that can contain all Features in a given FeatureCollection, optionally extending an existing Envelope.
		/// </summary>
		/// <param name="features">The FeatureCollection to measure.</param>
		/// <param name="extents">If this is present, <paramref name="features"/> will be measured against this envelope rather than a blank one.</param>
		/// <returns>An envelope that can contain all Features in the provided collection with dimensions at least as large as the optional Envelope <paramref name="extents"/>.</returns>
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

		/// <summary>
		/// Translate extents by given distances.
		/// </summary>
		/// <param name="x">Distance to offset in x-direction</param>
		/// <param name="y">Distance to offset in y-direction</param>
		internal void Offset(int x, int y)
		{
			MinX += x;
			MaxX += x;
			MinY += y;
			MaxY += y;
		}

		/// <summary>
		/// Find the smallest bounding box that can contain all provided GeometryObjects, optionally extending an existing Envelope.
		/// </summary>
		/// <param name="geoJsonObjects">Collection of IGeometrryObjects to  measure.</param>
		/// <param name="extents">If this is present, <paramref name="geoJsonObjects"/> will be measured against this envelope rather than a blank one.</param>
		/// <returns>An envelope that can contain all Features in the provided collection with dimensions at least as large as the optional Envelope <paramref name="extents"/>.</returns>
		/// <remarks>Will recurse through nested GeometryCollections.</remarks>
		public static Envelope FindExtents(IEnumerable<IGeometryObject> geoJsonObjects, Envelope extents = null)
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
						default:
							break;
					}
				}
			}
			return Extents;
		}

		/// <summary>
		/// Return an Envelope that contains a given position, optionally extending an existing Envelope to encompass it.
		/// </summary>
		/// <param name="position">Position to measure.</param>
		/// <param name="extents">Optional existing envelope to extend. If this is omitted, the returned envelope will have the same dimensions as <paramref name="position"/>.</param>
		/// <returns>An envelope that contains the <paramref name="position"/> parameter. If <paramref name="extents"/> is populated, that envelope will be extended to contain position.</returns>
		private static Envelope FindExtents(IPosition position, Envelope extents = null)
		{
			var Extents = new Envelope();
			if (extents != null)
			{
				Extents = extents;
			}
			Extents.MinX = new double[] { Extents.MinX, position.Longitude }.Where(v => v != 0).DefaultIfEmpty().Min();
			Extents.MinY = new double[] { Extents.MinY, position.Latitude }.Where(v => v != 0).DefaultIfEmpty().Min();
			Extents.MaxX = new double[] { Extents.MaxX, position.Longitude }.Where(v => v != 0).DefaultIfEmpty().Max();
			Extents.MaxY = new double[] { Extents.MaxY, position.Latitude }.Where(v => v != 0).DefaultIfEmpty().Max();
			return Extents;
		}
	}
}