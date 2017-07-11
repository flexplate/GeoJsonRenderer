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
			
			// Zoom to extents
			RotateAndScale(GeoJsonObjects, width, height);
			Envelope Extents2 = Envelope.FindExtents(GeoJsonObjects);

			// Rebase to zero
			RebaseGeometry(GeoJsonObjects, Extents2);

			// Create canvas
			using (Bitmap = new Bitmap(width, height))
			{
				using (Graphics = Graphics.FromImage(Bitmap))
				{

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
			}

		}


		/// <summary>
		/// Iterate through feature collection and rebase its coordinates around zero.
		/// </summary>
		/// <param name="features"></param>
		/// <param name="envelope"></param>
		private void RebaseGeometry(FeatureCollection features, Envelope envelope)
		{
			for (int f = 0; f < features.Features.Count; f++)
			{
				Feature Feature = features.Features[f];
				switch (Feature.Geometry.Type)
				{
					case GeoJSONObjectType.Point:
						Feature = new Feature(new GeoJSON.Net.Geometry.Point(RebasePosition(((GeoJSON.Net.Geometry.Point)Feature.Geometry).Coordinates, envelope)), Feature.Properties, Feature.Id != null ? Feature.Id : null);
						break;
					case GeoJSONObjectType.MultiPoint:
						{
							var MultiP = ((MultiPoint)Feature.Geometry);
							for (int i = 0; i < MultiP.Coordinates.Count; i++)
							{
								MultiP.Coordinates[i] = new GeoJSON.Net.Geometry.Point(RebasePosition(MultiP.Coordinates[i].Coordinates, envelope));
							}
							break;
						}
					case GeoJSONObjectType.LineString:
						{
							var Line = ((LineString)Feature.Geometry);
							for (int i = 0; i < Line.Coordinates.Count; i++) { Line.Coordinates[i] = RebasePosition(Line.Coordinates[i], envelope); }
							break;
						}
					case GeoJSONObjectType.MultiLineString:
						foreach (var Line in ((MultiLineString)Feature.Geometry).Coordinates)
						{
							for (int i = 0; i < Line.Coordinates.Count; i++)
							{ Line.Coordinates[i] = RebasePosition(Line.Coordinates[i], envelope); }
						}
						break;
					case GeoJSONObjectType.Polygon:
						foreach (var Line in ((Polygon)Feature.Geometry).Coordinates)
						{
							for (int i = 0; i < Line.Coordinates.Count; i++)
							{ Line.Coordinates[i] = RebasePosition(Line.Coordinates[i], envelope); }
						}
						break;
					case GeoJSONObjectType.MultiPolygon:
						foreach (var Poly in ((MultiPolygon)Feature.Geometry).Coordinates)
						{
							foreach (var Line in Poly.Coordinates)
							{
								for (int i = 0; i < Line.Coordinates.Count; i++)
								{ Line.Coordinates[i] = RebasePosition(Line.Coordinates[i], envelope); }
							}
						}
						break;
					case GeoJSONObjectType.GeometryCollection:
						// TODO
						break;
					case GeoJSONObjectType.Feature:
						FeatureCollection Collection = new FeatureCollection(new List<Feature>() { Feature });
						RebaseGeometry(Collection, envelope);
						break;
					case GeoJSONObjectType.FeatureCollection:
						RebaseGeometry((FeatureCollection)Feature.Geometry, envelope);
						break;
					default:
						break;
				}
			}
		}

		private IPosition RebasePosition(IPosition coordinates, Envelope envelope)
		{
			return new Position(coordinates.Latitude - envelope.MinX, coordinates.Longitude - envelope.MinY);
		}


		private void RotateAndScale(FeatureCollection geoJsonObjects, int width, int height)
		{
			Envelope Extents = Envelope.FindExtents(geoJsonObjects);
			double OutputAspect = width / (double)height;
			// Set rotate flag if one aspect > 1, but not both.
			bool Rotate = (Extents.AspectRatio > 1) ^ (OutputAspect > 1);
			double ScaleFactor;
			if (Rotate)
			{
				ScaleFactor = Extents.Width / height;
			}
			else
			{
				ScaleFactor = Extents.Width / width;
			}

			for (int i = 0; i < geoJsonObjects.Features.Count; i++)
			{
				Feature Feature = geoJsonObjects.Features[i];
				switch (Feature.Geometry.Type)
				{
					case GeoJSONObjectType.Point:
						{
							IPosition PointPosition = ((GeoJSON.Net.Geometry.Point)Feature.Geometry).Coordinates;
							if (Rotate) { PointPosition = RotatePosition90Degrees(PointPosition); }
							var Point = new GeoJSON.Net.Geometry.Point(ScalePosition(PointPosition, ScaleFactor));
							Feature = new Feature(Point, Feature.Properties, Feature.Id != null ? Feature.Id : null);
							break;
						}
					case GeoJSONObjectType.MultiPoint:
						{
							var MultiP = ((MultiPoint)Feature.Geometry);
							for (int j = 0; j < MultiP.Coordinates.Count; j++)
							{
								var Position = MultiP.Coordinates[j].Coordinates;
								if (Rotate) { Position = RotatePosition90Degrees(Position); }
								MultiP.Coordinates[j] = new GeoJSON.Net.Geometry.Point(ScalePosition(Position, ScaleFactor));
							}
							break;
						}
					case GeoJSONObjectType.LineString:
						{
							var Line = ((LineString)Feature.Geometry);
							for (int j = 0; j < Line.Coordinates.Count; j++)
							{
								var Position = Line.Coordinates[j];
								if (Rotate) { Position = RotatePosition90Degrees(Position); }
								Position = ScalePosition(Position, ScaleFactor);
							}
							break;
						}
					case GeoJSONObjectType.MultiLineString:
						foreach (var Line in ((MultiLineString)Feature.Geometry).Coordinates)
						{
							for (int j = 0; j < Line.Coordinates.Count; j++)
							{
								var Position = Line.Coordinates[j];
								if (Rotate) { Position = RotatePosition90Degrees(Position); }
								Position = ScalePosition(Position, ScaleFactor);
							}
						}
						break;
					case GeoJSONObjectType.Polygon:
						foreach (var Line in ((Polygon)Feature.Geometry).Coordinates)
						{
							for (int j = 0; j < Line.Coordinates.Count; j++)
							{
								if (Rotate) { Line.Coordinates[j] = RotatePosition90Degrees(Line.Coordinates[j]); }
								Line.Coordinates[j] = ScalePosition(Line.Coordinates[j], ScaleFactor);
							}
						}
						break;
					case GeoJSONObjectType.MultiPolygon:
						foreach (var Poly in ((MultiPolygon)Feature.Geometry).Coordinates)
						{
							foreach (var Line in Poly.Coordinates)
							{
								for (int j = 0; j < Line.Coordinates.Count; j++)
								{
									if (Rotate) { Line.Coordinates[j] = RotatePosition90Degrees(Line.Coordinates[j]); }
									Line.Coordinates[j] = ScalePosition(Line.Coordinates[j], ScaleFactor);
								}
							}
						}
						break;
					case GeoJSONObjectType.GeometryCollection:
						break;
					case GeoJSONObjectType.Feature:
						FeatureCollection Collection = new FeatureCollection(new List<Feature>(){ Feature });
						RotateAndScale(Collection, width, height);
						break;
					case GeoJSONObjectType.FeatureCollection:
						RotateAndScale((FeatureCollection)Feature.Geometry, width, height);
						break;
					default:
						break;
				}
			}

		}

		public IPosition RotatePosition90Degrees(IPosition position)
		{
			return new Position(-position.Longitude, position.Latitude, position.Altitude);
		}

		public IPosition ScalePosition(IPosition position, double scaleFactor)
		{
			if (position.Altitude != null)
			{
				return new Position(position.Latitude * scaleFactor, position.Longitude * scaleFactor, position.Altitude * scaleFactor);
			}
			return new Position(position.Latitude * scaleFactor, position.Longitude * scaleFactor);
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
					PointF PtF = new PointF() { X = (float)Position.Longitude, Y = (float)Position.Latitude };
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
