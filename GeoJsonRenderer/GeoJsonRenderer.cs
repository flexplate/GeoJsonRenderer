using GeoJSON.Net;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Therezin.GeoJsonRenderer
{
	/// <summary>
	/// Provides methods to render a GeoJSON strings to images.
	/// </summary>
	public class GeoJsonRenderer
	{
		private Bitmap OutputBitmap;
		private Graphics DrawingSurface;

		/// <summary>
		/// Default <seealso cref="Therezin.GeoJsonRenderer.DrawingStyle"/> to use when drawing Features.
		/// </summary>
		public DrawingStyle DefaultStyle { get; set; }

		/// <summary>
		/// Optional <seealso cref="DrawingStyle"/> to use when drawing Features, chosen by RenderGeoJson's AlternativeStyleFunction method parameter.
		/// </summary>
		public DrawingStyle OptionalStyle { get; set; }

		/// <summary>
		/// Instantiate a GeoJsonRenderer, optionally with different styles to the defaults.
		/// </summary>
		/// <param name="defaultStyle">A <seealso cref="DrawingStyle"/> to replace the default style.</param>
		/// <param name="optionalStyle">A <seealso cref="DrawingStyle"/> to replace the optional alternative style set by RenderGeoJson's AlternativeStyleFunction method parameter.</param>
		public GeoJsonRenderer(DrawingStyle defaultStyle = null, DrawingStyle optionalStyle = null)
		{
			if (defaultStyle != null)
			{
				DefaultStyle = defaultStyle;
			}
			else
			{
				DefaultStyle = new DrawingStyle(new Pen(Color.Green, 2.0f), null);
			}

			if (optionalStyle != null)
			{
				OptionalStyle = optionalStyle;
			}
			else
			{
				OptionalStyle = new DrawingStyle(new Pen(Color.Maroon, 2.0f), new SolidBrush(Color.Red));
			}
		}


		#region Rendering

		/// <summary>
		/// Render a GeoJSON string to a PNG image.
		/// </summary>
		/// <param name="json">GeoJSON string to render.</param>
		/// <param name="outputPath">Path to output file.</param>
		/// <param name="width">Desired width of output image in pixels.</param>
		/// <param name="height">Desired height of output image in pixels.</param>
		/// <param name="filterExpression">Method expression to filter JSON strings by.</param>
		/// <param name="alternativeStyleFunction">Method expression to determine which of 2 styles a feature should be drawn with.</param>
		/// <returns></returns>
		public bool RenderGeoJson(string json, string outputPath, int width, int height, Func<Feature, bool> filterExpression = null, Func<Feature, bool> alternativeStyleFunction = null)
		{
			var GeoJsonObjects = JsonConvert.DeserializeObject<FeatureCollection>(json);

			if (filterExpression != null)
			{
				try
				{
					var FilteredObjects = new FeatureCollection(GeoJsonObjects.Features.Where(filterExpression).ToList());
					GeoJsonObjects = FilteredObjects;
				}
				catch { };
			}

			// Zoom to extents
			GeoJsonObjects = RotateAndScaleFeatures(GeoJsonObjects, width, height);
			Envelope Extents = Envelope.FindExtents(GeoJsonObjects);

			// Rebase to zero
			GeoJsonObjects = TranslateFeatures(GeoJsonObjects, Extents);

			// Create canvas
			using (OutputBitmap = new Bitmap(width, height))
			{
				using (DrawingSurface = Graphics.FromImage(OutputBitmap))
				{
					DrawingSurface.FillRectangle(Brushes.White, new Rectangle(0, 0, width, height));
					foreach (var Item in GeoJsonObjects.Features)
					{
						if (alternativeStyleFunction != null && alternativeStyleFunction(Item) == true)
						{
							DrawGeometry(Item.Geometry, OptionalStyle);
						}
						else
						{
							DrawGeometry(Item.Geometry, DefaultStyle);
						}
					}
					OutputBitmap.Save(outputPath);
					return true;
				}
			}
		}

		/// <summary>
		/// Render an array of GeoJSON strings to a PNG image.
		/// </summary>
		/// <param name="json">GeoJSON strings to render. Strings are processed in order.
		/// The extents of the first layer are used for all further layers.</param>
		/// <param name="outputPath">Path to output file.</param>
		/// <param name="width">Desired width of output image in pixels.</param>
		/// <param name="height">Desired height of output image in pixels.</param>
		/// <param name="filterExpression">Method expression to filter JSON strings by.</param>
		/// <param name="alternativeStyleFunction">Method expression to determine which of 2 styles a feature should be drawn with.</param>
		/// <returns></returns>
		public bool RenderGeoJson(string[] json, string outputPath, int width, int height, Func<Feature, bool> filterExpression = null, Func<Feature, bool> alternativeStyleFunction = null)
		{
			var Extents = new Envelope();
			var Objects = new List<FeatureCollection>();

			for (int i = 0; i < json.Length; i++)
			{
				var GeoJsonObjects = JsonConvert.DeserializeObject<FeatureCollection>(json[i]);

				if (filterExpression != null)
				{
					try
					{
						var FilteredObjects = new FeatureCollection(GeoJsonObjects.Features.Where(filterExpression).ToList());
						GeoJsonObjects = FilteredObjects;
					}
					catch { };
				}
				Objects.Add(GeoJsonObjects);
			}
			// Get extents of largest collection.
			Extents = Envelope.FindExtents(Objects);
			for (int i = 0; i < Objects.Count; i++)
			{
				Objects[i] = RotateAndScaleFeatures(Objects[i], width, height, extents: Extents);
			}

			// Get extents of largest collection again.
			Extents = Envelope.FindExtents(Objects);
			for (int i = 0; i < Objects.Count; i++)
			{
				// Rebase to zero
				Objects[i] = TranslateFeatures(Objects[i], Extents);
			}

			// Create canvas
			using (OutputBitmap = new Bitmap(width, height))
			{
				using (DrawingSurface = Graphics.FromImage(OutputBitmap))
				{
					DrawingSurface.FillRectangle(Brushes.White, new Rectangle(0, 0, width, height));
					foreach (var GeoJsonObjects in Objects)
					{
						foreach (var Item in GeoJsonObjects.Features)
						{
							if (alternativeStyleFunction != null && alternativeStyleFunction(Item) == true)
							{
								DrawGeometry(Item.Geometry, OptionalStyle);
							}
							else
							{
								DrawGeometry(Item.Geometry, DefaultStyle);
							}
						}
					}
					OutputBitmap.Save(outputPath);
					return true;
				}
			}
		}

		#endregion

		#region Transformation


		/// <summary>
		/// Rotate and scale all features in a FeatureCollection to fit inside a box.
		/// </summary>
		/// <param name="features">FeatureCollection to rotate.</param>
		/// <param name="width">Width of bounding box.</param>
		/// <param name="height">Height of bounding box.</param>
		/// <param name="rotateRadians">Radians to rotate by. Defaults to 90 degrees.</param>
		/// <param name="rotate">Whether to rotate the features. If this is null, features will be rotated if input and output are different aspects.</param>
		/// <param name="extents">Extents to use. If null, will use extents of "<paramref name="features"/>" parameter.</param>
		/// <returns></returns>
		public FeatureCollection RotateAndScaleFeatures(FeatureCollection features, int width, int height, double rotateRadians = 4.7124, bool? rotate = null, Envelope extents = null)
		{
			Envelope Extents = extents != null ? extents : Envelope.FindExtents(features);

			double OutputAspect = width / (double)height;
			// If we're not sure whether to rotate, set rotate flag if one aspect > 1, but not both.
			bool Rotate = rotate != null ? (bool)rotate : (Extents.AspectRatio > 1) ^ (OutputAspect > 1);
			double ScaleFactor;
			if (Rotate)
			{
				ScaleFactor = height / Extents.Width;
			}
			else
			{
				ScaleFactor = width / Extents.Width;
			}
			var OutCollection = new FeatureCollection();
			for (int i = 0; i < features.Features.Count; i++)
			{
				Feature InFeature = features.Features[i];
				OutCollection.Features.Add(new Feature(RotateAndScaleGeometry(InFeature.Geometry, ScaleFactor, rotateRadians), InFeature.Properties, InFeature.Id != null ? InFeature.Id : null));
			}
			return OutCollection;
		}

		/// <summary>
		/// Rotate and/or scale a Geometry Object around the origin (0,0).
		/// </summary>
		/// <param name="geometry">Geometry object to transform.</param>
		/// <param name="scaleFactor">Scaling to apply. Omit or set to 1 to not scale.</param>
		/// <param name="rotateRadians">Rotation to apply, in radians. Omit or set to zero to not rotate.</param>
		/// <returns></returns>
		public IGeometryObject RotateAndScaleGeometry(IGeometryObject geometry, double scaleFactor = 1, double rotateRadians = 0)
		{
			switch (geometry.Type)
			{
				case GeoJSONObjectType.Point:
					{
						IPosition PointPosition = ((GeoJSON.Net.Geometry.Point)geometry).Coordinates;
						if (rotateRadians != 0) { PointPosition = RotatePositionAroundZero(PointPosition, rotateRadians); }
						var Point = new GeoJSON.Net.Geometry.Point(ScalePosition(PointPosition, scaleFactor));
						return Point;
					}
				case GeoJSONObjectType.MultiPoint:
					{
						var MultiP = ((MultiPoint)geometry);
						var OutMulti = new MultiPoint();
						for (int j = 0; j < MultiP.Coordinates.Count; j++)
						{
							var Position = MultiP.Coordinates[j].Coordinates;
							if (rotateRadians != 0) { Position = RotatePositionAroundZero(Position, rotateRadians); }
							OutMulti.Coordinates.Add(new GeoJSON.Net.Geometry.Point(ScalePosition(Position, scaleFactor)));
						}
						return OutMulti;
					}
				case GeoJSONObjectType.LineString:
					{
						var Line = ((LineString)geometry);
						var Positions = new List<IPosition>();
						for (int j = 0; j < Line.Coordinates.Count; j++)
						{
							var Position = Line.Coordinates[j];
							if (rotateRadians != 0) { Position = RotatePositionAroundZero(Position, rotateRadians); }
							Positions.Add(ScalePosition(Position, scaleFactor));
						}
						return new LineString(Positions);
					}
				case GeoJSONObjectType.MultiLineString:
					{
						var Lines = new List<LineString>();
						foreach (var Line in ((MultiLineString)geometry).Coordinates)
						{
							var Positions = new List<IPosition>();
							for (int j = 0; j < Line.Coordinates.Count; j++)
							{
								var Position = Line.Coordinates[j];
								if (rotateRadians != 0) { Position = RotatePositionAroundZero(Position, rotateRadians); }
								Positions.Add(ScalePosition(Position, scaleFactor));
							}
							Lines.Add(new LineString(Positions));
						}
						return new MultiLineString(Lines);
					}
				case GeoJSONObjectType.Polygon:
					{
						var Lines = new List<LineString>();
						foreach (var Line in ((Polygon)geometry).Coordinates)
						{
							var Positions = new List<IPosition>();
							for (int j = 0; j < Line.Coordinates.Count; j++)
							{
								if (rotateRadians != 0) { Line.Coordinates[j] = RotatePositionAroundZero(Line.Coordinates[j], rotateRadians); }
								Positions.Add(ScalePosition(Line.Coordinates[j], scaleFactor));
							}
							Lines.Add(new LineString(Positions));
						}
						return new Polygon(Lines);
					}
				case GeoJSONObjectType.MultiPolygon:
					var Polys = new MultiPolygon();
					foreach (var Poly in ((MultiPolygon)geometry).Coordinates)
					{
						var Lines = new List<LineString>();
						foreach (var Line in Poly.Coordinates)
						{
							var Positions = new List<IPosition>();
							for (int j = 0; j < Line.Coordinates.Count; j++)
							{
								if (rotateRadians != 0) { Line.Coordinates[j] = RotatePositionAroundZero(Line.Coordinates[j], rotateRadians); }
								Positions.Add(ScalePosition(Line.Coordinates[j], scaleFactor));
							}
							Lines.Add(new LineString(Positions));
						}
						Polys.Coordinates.Add(new Polygon(Lines));
					}
					return Polys;
				case GeoJSONObjectType.GeometryCollection:
					var Geometries = new List<IGeometryObject>();
					foreach (var Geometry in ((GeometryCollection)geometry).Geometries)
					{
						Geometries.Add(RotateAndScaleGeometry(Geometry, scaleFactor, rotateRadians));
					}
					return new GeometryCollection(Geometries);
				default:
					return null;
			}
		}
		#region Translate

		/// <summary>
		/// Iterate through feature collection and rebase its coordinates' origins to the minima of an Envelope.
		/// </summary>
		/// <param name="features">FeatureCollection to translate.</param>
		/// <param name="envelope">Envelope to use as origin for translation.</param>
		/// <returns>New FeatureCollection with coordinates of its component Features'Geometries rebased to the minima of an envelope.</returns>
		private FeatureCollection TranslateFeatures(FeatureCollection features, Envelope envelope)
		{
			var OutList = new List<Feature>();
			for (int f = 0; f < features.Features.Count; f++)
			{
				var Feature = features.Features[f];
				OutList.Add(new Feature(TranslateGeometry(Feature.Geometry, envelope), Feature.Properties, features.Features[f].Id != null ? features.Features[f].Id : null));
			}
			return new FeatureCollection(OutList);
		}

		/// <summary>
		/// Translate the coordinates of a Geometry Object to originate at the minima of an envelope.
		/// </summary>
		/// <param name="geometry">Geometry to translate.</param>
		/// <param name="envelope">Envelope to use as origin for translation.</param>
		/// <returns>New Geometry Object of the same type, with coordinates rebased to the minima of an envelope.</returns>
		/// <remarks>Will recurse through a GeometryCollection.</remarks>
		private IGeometryObject TranslateGeometry(IGeometryObject geometry, Envelope envelope)
		{
			switch (geometry.Type)
			{
				case GeoJSONObjectType.Point:
					return new GeoJSON.Net.Geometry.Point(TranslatePosition(((GeoJSON.Net.Geometry.Point)geometry).Coordinates, envelope));
				case GeoJSONObjectType.MultiPoint:
					{
						var MultiP = ((MultiPoint)geometry);
						for (int i = 0; i < MultiP.Coordinates.Count; i++)
						{
							MultiP.Coordinates[i] = new GeoJSON.Net.Geometry.Point(TranslatePosition(MultiP.Coordinates[i].Coordinates, envelope));
						}
						return MultiP;
					}
				case GeoJSONObjectType.LineString:
					{
						var Line = ((LineString)geometry);
						for (int i = 0; i < Line.Coordinates.Count; i++) { Line.Coordinates[i] = TranslatePosition(Line.Coordinates[i], envelope); }
						return Line;
					}
				case GeoJSONObjectType.MultiLineString:
					{
						var Lines = new List<LineString>();
						foreach (var Line in ((MultiLineString)geometry).Coordinates)
						{
							for (int i = 0; i < Line.Coordinates.Count; i++)
							{
								Line.Coordinates[i] = TranslatePosition(Line.Coordinates[i], envelope);
							}
							Lines.Add(Line);
						}
						return new MultiLineString(Lines);
					}
				case GeoJSONObjectType.Polygon:
					{
						var Lines = new List<LineString>();
						foreach (var Line in ((Polygon)geometry).Coordinates)
						{
							for (int i = 0; i < Line.Coordinates.Count; i++)
							{
								Line.Coordinates[i] = TranslatePosition(Line.Coordinates[i], envelope);
							}
							Lines.Add(Line);
						}
						return new Polygon(Lines);
					}
				case GeoJSONObjectType.MultiPolygon:
					{
						var Polys = new List<Polygon>();
						foreach (var Poly in ((MultiPolygon)geometry).Coordinates)
						{
							var Lines = new List<LineString>();
							foreach (var Line in Poly.Coordinates)
							{
								for (int i = 0; i < Line.Coordinates.Count; i++)
								{
									Line.Coordinates[i] = TranslatePosition(Line.Coordinates[i], envelope);
								}
								Lines.Add(Line);
							}
							Polys.Add(new Polygon(Lines));
						}
						return new MultiPolygon(Polys);
					}
				case GeoJSONObjectType.GeometryCollection:
					var Geometries = new List<IGeometryObject>();
					foreach (var Geometry in ((GeometryCollection)geometry).Geometries)
					{
						Geometries.Add(TranslateGeometry(Geometry, envelope));
					}
					return new GeometryCollection(Geometries);
				default:
					return null;
			}
		}

		/// <summary>
		/// Translate the coordinates of a Position to be based on the minima of an envelope.
		/// </summary>
		/// <param name="coordinates">Position to rebase.</param>
		/// <param name="envelope">Envelope whose minima we base <paramref name="coordinates"/>' coordinates off.</param>
		/// <returns></returns>
		private IPosition TranslatePosition(IPosition coordinates, Envelope envelope)
		{
			return new Position(coordinates.Latitude - envelope.MinX, coordinates.Longitude - envelope.MinY);
		}

		#endregion

		#region Rotate

		/// <summary>
		/// Rotate a position around the origin (0,0).
		/// </summary>
		/// <param name="position">IPosition object to rotate.</param>
		/// <param name="theta">Angle to rotate <paramref name="position"/> by, in radians.</param>
		/// <returns>New Position with the Latitude and Longitude rotated by <paramref name="theta"/> radians.</returns>
		public IPosition RotatePositionAroundZero(IPosition position, double theta)
		{
			var NewX = Math.Cos(theta) * position.Latitude - Math.Sin(theta) * position.Longitude;
			var NewY = Math.Sin(theta) * position.Latitude + Math.Cos(theta) * position.Longitude;
			return new Position(NewX, NewY, position.Altitude);
		}

		#endregion

		#region Scale

		/// <summary>
		/// Scale an IPosition in 2 or 3 dimensions from (0,0[,0]).
		/// </summary>
		/// <param name="position">IPosition object to scale.</param>
		/// <param name="scaleFactor">Floating-point value to scale by.</param>
		/// <returns></returns>
		public IPosition ScalePosition(IPosition position, double scaleFactor)
		{
			if (position.Altitude != null)
			{
				return new Position(position.Latitude * scaleFactor, position.Longitude * scaleFactor, position.Altitude * scaleFactor);
			}
			return new Position(position.Latitude * scaleFactor, position.Longitude * scaleFactor);
		}

		#endregion

		#endregion

		#region Drawing

		/// <summary>
		/// Recursively draw a geometry object.
		/// </summary>
		/// <param name="geometry">GeoJSON Geometry Object to draw.</param>
		/// <param name="style">The DrawingStyle to draw <paramref name="geometry"/> with.</param>
		/// <remarks>Supports filling polygons if <paramref name="style"/>'s FillBrush property is non-null.</remarks>
		private void DrawGeometry(IGeometryObject geometry, DrawingStyle style)
		{
			switch (geometry.Type)
			{
				case GeoJSONObjectType.Point:
					var Position = ((GeoJSON.Net.Geometry.Point)geometry).Coordinates;
					PointF PtF = new PointF() { X = (float)Position.Longitude, Y = (float)Position.Latitude };
					DrawingSurface.DrawLine(style.LinePen, PtF, PtF);
					break;
				case GeoJSONObjectType.MultiPoint:
					foreach (var Points in ((MultiPoint)geometry).Coordinates) { DrawGeometry(Points, style); }
					break;
				case GeoJSONObjectType.LineString:
					DrawingSurface.DrawLines(style.LinePen, ConvertPositionsToPoints(((LineString)geometry).Coordinates));
					break;
				case GeoJSONObjectType.MultiLineString:
					foreach (var Line in ((MultiLineString)geometry).Coordinates) { DrawGeometry(Line, style); }
					break;
				case GeoJSONObjectType.Polygon:
					foreach (var PolyLine in ((Polygon)geometry).Coordinates)
					{
						DrawingSurface.DrawPolygon(style.LinePen, ConvertPositionsToPoints(PolyLine.Coordinates));
						if (style.FillBrush != null)
						{
							DrawingSurface.FillPolygon(style.FillBrush, ConvertPositionsToPoints(PolyLine.Coordinates));
						}
					}
					break;
				case GeoJSONObjectType.MultiPolygon:
					foreach (var Polygons in ((MultiPolygon)geometry).Coordinates) { DrawGeometry(Polygons, style); }
					break;
				case GeoJSONObjectType.GeometryCollection:
					foreach (var Geo in ((GeometryCollection)geometry).Geometries) { DrawGeometry(Geo, style); }
					break;
				default:
					break;
			}
					}

		#endregion

		#region Helpers

		/// <summary>
		/// Convert a List of Positions into an array of PointFs.
		/// </summary>
		private PointF[] ConvertPositionsToPoints(List<IPosition> positions)
		{
			var OutList = new List<PointF>();
			for (int i = 0; i < positions.Count; i++)
			{
				OutList.Add(new PointF() { X = (int)positions[i].Latitude, Y = (int)positions[i].Longitude });
			}
			return OutList.ToArray();
		}

		#endregion

	}
}
