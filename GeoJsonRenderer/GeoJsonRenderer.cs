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
			Envelope Extents2 = Envelope.FindExtents(GeoJsonObjects);

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
							for (int i = 0; i < Line.Coordinates.Count; i++) { ((LineString)Feature.Geometry).Coordinates[i] = RebasePosition(Line.Coordinates[i], envelope); }
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
			return new Position(coordinates.Longitude - envelope.MinX, coordinates.Latitude - envelope.MinY);
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
