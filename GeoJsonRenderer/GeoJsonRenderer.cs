using GeoJSON.Net;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Therezin.GeoJsonRenderer
{
	/// <summary>
	/// Provides methods to render a GeoJSON strings to images.
	/// </summary>
	public class GeoJsonRenderer : IDisposable
	{
		private Bitmap OutputBitmap;
		private Graphics DrawingSurface;

		private DrawingStyle _defaultStyle;
		private DrawingStyle _optionalStyle;

		/// <summary>List of FeatureCollections to render, drawn FIFO.</summary>
		public List<FeatureCollection> Layers;

		/// <summary>Method expression to determine which of 2 styles a feature should be drawn with.</summary>
		public Func<Feature, bool> AlternativeStyleFunction = null;

		/// <summary>Default <seealso cref="Therezin.GeoJsonRenderer.DrawingStyle"/> to use when drawing Features.</summary>
		public DrawingStyle DefaultStyle
		{
			get { return _defaultStyle; }
			set
			{
				if (value != null)
				{
					_defaultStyle = value;
				}
				else
				{
					_defaultStyle = new DrawingStyle(new Pen(Color.Green, 2.0f), null);
				}
			}
		}

		/// <summary>Optional <seealso cref="DrawingStyle"/> to use when drawing Features, chosen by <seealso cref="AlternativeStyleFunction"/> Func property.</summary>
		public DrawingStyle OptionalStyle
		{
			get { return _optionalStyle; }
			set
			{
				if (value != null)
				{
					_optionalStyle = value;
				}
				else
				{
					_optionalStyle = new DrawingStyle(new Pen(Color.Maroon, 2.0f), new SolidBrush(Color.Red));
				}
			}
		}


		/// <summary>
		/// Instantiate a GeoJsonRenderer, optionally with different styles to the defaults.
		/// </summary>
		/// <param name="defaultStyle">A <seealso cref="DrawingStyle"/> to replace the default style.</param>
		/// <param name="optionalStyle">A <seealso cref="DrawingStyle"/> to replace the optional alternative style set by RenderGeoJson's AlternativeStyleFunction method parameter.</param>
		public GeoJsonRenderer(DrawingStyle defaultStyle = null, DrawingStyle optionalStyle = null)
		{
			DefaultStyle = defaultStyle;
			OptionalStyle = optionalStyle;

			Layers = new List<FeatureCollection>();
		}


		#region Data Input

		/// <summary>
		/// Parse a GeoJSON string and load in into the Layers collection.
		/// </summary>
		/// <param name="json">GeoJSON string to parse</param>
		public void LoadGeoJson(string json)
		{
			Layers.Add(JsonConvert.DeserializeObject<FeatureCollection>(json));
		}

		/// <summary>
		/// Parse a selection of GeoJSON strings and load them into the Layers collection.
		/// </summary>
		/// <param name="jsonArray">String array to parse.</param>
		public void LoadGeoJson(string[] jsonArray)
		{
			for (int i = 0; i < jsonArray.Length; i++)
			{
				Layers.Add(JsonConvert.DeserializeObject<FeatureCollection>(jsonArray[i]));
			}
		}

		#endregion

		#region Transformation

		/// <summary>
		/// Rotate, scale and translate the Layers collection to fit within the specified pixel dimensions.
		/// </summary>
		/// <param name="width">Desired width of output image in pixels, including border size (if any).</param>
		/// <param name="height">Desired height of output image in pixels, including border size (if any).</param>
		/// <param name="borderSize">Size of border to add to output image.</param>
		public void FitLayersToPage(int width, int height, int borderSize = 0)
		{
			int ContentWidth = width;
			int ContentHeight = height;
			if (borderSize > 0)
			{
				ContentWidth = ContentWidth - borderSize * 2;
				ContentHeight = ContentHeight - borderSize * 2;
			}

			Envelope Extents = Envelope.FindExtents(Layers);
			for (int i = 0; i < Layers.Count; i++)
			{
				Layers[i] = RotateAndScaleFeatures(Layers[i], ContentWidth, ContentHeight, extents: Extents);
			}
			Extents = Envelope.FindExtents(Layers);
			if (borderSize > 0)
			{
				Extents.Offset(-borderSize, -borderSize);
			}
			for (int i = 0; i < Layers.Count; i++)
			{
				Layers[i] = TranslateFeatures(Layers[i], Extents);
			}
		}

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
			Envelope Extents = extents ?? Envelope.FindExtents(features);

			double OutputAspect = width / (double)height;
			// If we're not sure whether to rotate, set rotate flag if one aspect > 1, but not both.
			bool Rotate = rotate ?? (Extents.AspectRatio > 1) ^ (OutputAspect > 1);
			if (Rotate == false) { rotateRadians = 0; }
			double ScaleFactor = Math.Max(width, height) / Math.Max(Extents.Width, Extents.Height);

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
			return new Position(coordinates.Longitude + (0 - envelope.MinX), coordinates.Latitude + (0 - envelope.MinY));
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

		#region Rendering

		/// <summary>
		/// Render our geometry collection to a Bitmap's Graphics object.
		/// </summary>
		private void RenderLayers(int width, int height)
		{
			OutputBitmap = new Bitmap(width, height);
			DrawingSurface = Graphics.FromImage(OutputBitmap);

			// Graphics origin is top-left, so we must flip its coordinate system.
			DrawingSurface.TranslateTransform(0, height);
			DrawingSurface.ScaleTransform(1, -1);

			// Fill canvas with white.
			DrawingSurface.FillRectangle(Brushes.White, new Rectangle(0, 0, OutputBitmap.Width, OutputBitmap.Height));

			foreach (var Layer in Layers)
			{
				foreach (var Item in Layer.Features)
				{
					if (AlternativeStyleFunction != null && AlternativeStyleFunction(Item) == true)
					{
						DrawGeometry(Item.Geometry, OptionalStyle);
					}
					else
					{
						DrawGeometry(Item.Geometry, DefaultStyle);
					}
				}
			}

		}

		/// <summary>
		/// Render the <see cref="Layers"/> collection to an image file.
		/// </summary>
		/// <param name="path">Path to output file.</param>
		/// <param name="width">Desired width of output image in pixels.</param>
		/// <param name="height">Desired height of output image in pixels.</param>
		public bool SaveImage(string path, int width, int height)
		{
			RenderLayers(width, height);
			OutputBitmap.Save(path);
			return true;
		}

		/// <summary>
		/// Render the <see cref="Layers"/> collection to an image in the form of a MemoryStream.
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <returns></returns>
		public MemoryStream ToStream(int width, int height)
		{
			RenderLayers(width, height);
			var OutputStream = new MemoryStream();
			OutputBitmap.Save(OutputStream, ImageFormat.Png);
			return OutputStream;
		}

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

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		///<summary>This code added to correctly implement the disposable pattern.</summary> 
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					DrawingSurface.Dispose();
					OutputBitmap.Dispose();
				}
				disposedValue = true;
			}
		}

		///<summary>This code added to correctly implement the disposable pattern.</summary> 
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}
		#endregion

	}
}
