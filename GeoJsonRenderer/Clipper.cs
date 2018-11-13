using GeoJSON.Net.Geometry;

namespace Therezin.GeoJsonRenderer
{
    /// <summary>
    /// Uses modified Liang-Barsky algorithm to detect intersection between geometry and envelopes.
    /// </summary>
    public class Clipper
    {
        /// <summary>
        /// Test whether a GeometryObject intersects an Envelope.
        /// </summary>
        /// <param name="geometry">Geometry to check against <paramref name="envelope"/>.</param>
        /// <param name="envelope">Envelope to test intersections with.</param>
        /// <returns>True if <paramref name="geometry"/> intersects <paramref name="envelope"/>.</returns>
        public static bool GeometryIntersectsEnvelope(IGeometryObject geometry, Envelope envelope)
        {
            bool ReturnValue = false;
            switch (geometry.Type)
            {
                case GeoJSON.Net.GeoJSONObjectType.Point:
                    ReturnValue = PositionIsInsideEnvelope(((Point)geometry).Coordinates, envelope);
                    break;
                case GeoJSON.Net.GeoJSONObjectType.MultiPoint:
                    foreach (var Position in ((MultiPoint)geometry).Coordinates)
                    {
                        if (PositionIsInsideEnvelope(Position.Coordinates, envelope))
                        {
                            ReturnValue = true;
                        }
                    }
                    break;
                case GeoJSON.Net.GeoJSONObjectType.LineString:
                    // Break LineString into single segments
                    for (int i = 0; i < ((LineString)geometry).Coordinates.Count - 1; i++)
                    {
                        if (LineIntersectsEnvelope(((LineString)geometry).Coordinates[i], ((LineString)geometry).Coordinates[i + 1], envelope))
                        {
                            ReturnValue = true;
                        }
                    }
                    break;
                case GeoJSON.Net.GeoJSONObjectType.MultiLineString:
                    foreach (LineString Line in ((MultiLineString)geometry).Coordinates)
                    {
                        if (GeometryIntersectsEnvelope(Line, envelope))
                        {
                            ReturnValue = true;
                        }
                    }
                    break;
                case GeoJSON.Net.GeoJSONObjectType.Polygon:
                    foreach (LineString Line in ((Polygon)geometry).Coordinates)
                    {
                        if (GeometryIntersectsEnvelope(Line, envelope))
                        {
                            ReturnValue = true;
                        }
                    }
                    break;
                case GeoJSON.Net.GeoJSONObjectType.MultiPolygon:
                    foreach (Polygon Poly in ((MultiPolygon)geometry).Coordinates)
                    {
                        if (GeometryIntersectsEnvelope(Poly, envelope))
                        {
                            ReturnValue = true;
                        }
                    }
                    break;
                case GeoJSON.Net.GeoJSONObjectType.GeometryCollection:
                    foreach (IGeometryObject Geo in ((GeometryCollection)geometry).Geometries)
                    {
                        if (GeometryIntersectsEnvelope(Geo, envelope))
                        {
                            ReturnValue = true;
                        }
                    }
                    break;
                default:
                    break;
            }
            return ReturnValue;
        }

        /// <summary>
        /// Check to see if a point is inside the bounds of an Envelope.
        /// </summary>
        /// <param name="position">Point to check against <paramref name="envelope"/>.</param>
        /// <param name="envelope">Envelope to check against <paramref name="position"/>.</param>
        /// <returns>True if <paramref name="position"/> is inside <paramref name="envelope"/>.</returns>
        public static bool PositionIsInsideEnvelope(IPosition position, Envelope envelope)
        {
            if (position.Longitude > envelope.MinX && position.Longitude < envelope.MaxX)
            {
                if (position.Latitude > envelope.MinY && position.Latitude < envelope.MaxY)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check to see if a line intersects an Envelope using the Liang-Barsky algorithm.
        /// </summary>
        /// <param name="start">Start point of the line.</param>
        /// <param name="end">Endpoint of the line.</param>
        /// <param name="envelope">Envelope to intersect with.</param>
        /// <returns>True if line intersects <paramref name="envelope"/>.</returns>
        /// <remarks>Heavily based on http://www.skytopia.com/project/articles/compsci/clipping.html. </remarks>
        public static bool LineIntersectsEnvelope(IPosition start, IPosition end, Envelope envelope)
        {
            // Trivial acceptance: we want to accept lines with an endpoint inside the envelope.
            if (PositionIsInsideEnvelope(start, envelope) || PositionIsInsideEnvelope(end, envelope)) { return true; }

            double T0 = 0.0;
            double T1 = 1.0;
            double XDelta = end.Longitude - start.Longitude;
            double YDelta = end.Latitude - start.Latitude;
            double P = 0;
            double Q = 0;
            double R;

            for (int edge = 0; edge < 4; edge++)
            {
                // Traverse through left, right, bottom, top edges.
                switch (edge)
                {
                    case 0:
                        P = -XDelta;
                        Q = -((double)envelope.MinX - start.Longitude);
                        break;
                    case 1:
                        P = XDelta;
                        Q = ((double)envelope.MaxX - start.Longitude);
                        break;
                    case 2:
                        P = -YDelta;
                        Q = -((double)envelope.MinY - start.Latitude);
                        break;
                    case 3:
                        P = YDelta;
                        Q = ((double)envelope.MaxY - start.Latitude);
                        break;
                }

                R = Q / P;
                if (P == 0 && Q < 0) { return false; }   // Don't draw line at all. (parallel line outside)

                if (P < 0)
                {
                    if (R > T1) { return false; }  // Don't draw line at all.
                    else if (R > T0) { T0 = R; }       // Line is clipped!
                }
                else if (P > 0)
                {
                    if (R < T0) { return false; }      // Don't draw line at all.
                    else if (R < T1) { T1 = R; }       // Line is clipped!
                }
            }
            return true;        // (clipped) line is drawn
        }

    }
}

