using GeoJSON.Net;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace Therezin.GeoJsonRenderer
{
    /// <summary>
    /// Provides methods to render a GeoJSON strings to images.
    /// </summary>
    public class GeoJsonRenderer : IDisposable
    {
        #region Fields

        private Graphics drawingSurface;

        private DrawingStyle defaultStyle;

        private int canvasWidth;
        private int canvasHeight;

        private int pageWidth;
        private int pageHeight;

        private int borderSize;
        private int pageOverlap;

        private int originX;
        private int originY;

        private bool cropped;
        private bool multiPage;
        private bool pageRotated = false;

        #endregion

        #region Constructor and events

        /// <summary>
        /// Instantiate a GeoJsonRenderer, optionally with different styles to the defaults.
        /// </summary>
        /// <param name="defaultStyle">A <seealso cref="DrawingStyle"/> to replace the default style.</param>
        public GeoJsonRenderer(DrawingStyle defaultStyle = null)
        {
            DefaultStyle = defaultStyle;
            canvasWidth = -1;
            canvasHeight = -1;
            Layers = new List<Layer>();
        }

        /// <summary>Raised immediately before drawing each feature to facilitate custom styling.</summary>
        public event EventHandler<DrawingFeatureEventArgs> DrawingFeature;

        #endregion

        #region Properties

        /// <summary>List of FeatureCollections to render, drawn FIFO.</summary>
        public List<Layer> Layers;

        /// <summary>Default <seealso cref="Therezin.GeoJsonRenderer.DrawingStyle"/> to use when drawing Features.</summary>
        public DrawingStyle DefaultStyle
        {
            get { return defaultStyle; }
            set
            {
                if (value != null)
                {
                    defaultStyle = value;
                }
                else
                {
                    defaultStyle = new DrawingStyle(new Pen(Color.Green, 2.0f), null);
                }
            }
        }

        #endregion

        #region Data Input

        /// <summary>
        /// Parse a GeoJSON string and load in into the Layers collection.
        /// </summary>
        /// <param name="json">GeoJSON string to parse</param>
        public void LoadGeoJson(string json)
        {
            Layers.Add(JsonConvert.DeserializeObject<Layer>(json, new LayerDeserializer()));
        }

        /// <summary>
        /// Parse a selection of GeoJSON strings and load them into the Layers collection.
        /// </summary>
        /// <param name="jsonArray">String array to parse.</param>
        public void LoadGeoJson(string[] jsonArray)
        {
            for (int i = 0; i < jsonArray.Length; i++)
            {
                Layers.Add(JsonConvert.DeserializeObject<Layer>(jsonArray[i], new LayerDeserializer()));
            }
        }

        /// <summary>
        /// Parse a collection of GeoJSON strings and load them into the Layers collection.
        /// </summary>
        /// <param name="layers">Collection to parse.</param>
        public void LoadGeoJson(IEnumerable<string> layers)
        {
            foreach (var json in layers)
            {
                Layers.Add(JsonConvert.DeserializeObject<Layer>(json, new LayerDeserializer()));
            }
        }

        #endregion

        #region Transformation

        /// <summary>
        /// Fit GeoJSON layers to a page, or split across multiple pages, according to a scaling threshold.
        /// </summary>
        /// <param name="width">Width of the output page in pixels.</param>
        /// <param name="height">Height of the output page in pixels.</param>
        /// <param name="maximumScalingThreshold">If the scaling factor is below this threshold the output will be split. Tests recursively.</param>
        /// <param name="minimumScalingThreshold">Optional minimum scaling threshold to prevent large numbers of output pages and corresponding huge RAM usage.</param>
        /// <param name="border">Size of the border (if any) in pixels.</param>
        /// <param name="overlap">Amount to overlap from one page to the next.</param>
        public void Paginate(int width, int height, double maximumScalingThreshold, double minimumScalingThreshold = 0, int border = 0, int overlap = 0)
        {
            borderSize = border;
            pageWidth = width;
            pageHeight = height;
            pageOverlap = overlap;

            Envelope Extents = Envelope.FindExtents(Layers);
            canvasWidth = pageWidth - 2 * borderSize - 2 * overlap;
            canvasHeight = pageHeight - 2 * borderSize - 2 * overlap;
            multiPage = true;

            double ScaleFactor = Math.Min(canvasWidth / (double)Extents.Width, canvasHeight / (double)Extents.Height);

            // Keep zooming until we get a ScaleFactor between the thresholds.
            while (ScaleFactor < maximumScalingThreshold)
            {
                // Calculate new ScaleFactor in advance
                int OriginalWidth = canvasWidth;
                int TempWidth = canvasHeight * 2 - borderSize * 2 - overlap * 2;
                int TempHeight = TempWidth - borderSize * 2 - overlap * 2;
                double NewScaleFactor = Math.Min(canvasWidth / (double)Extents.Width, canvasHeight / (double)Extents.Height);

                if (NewScaleFactor > minimumScalingThreshold)
                {
                    // Rotate 90° and double, like going A4 to A3 etc
                    canvasWidth = TempWidth;
                    canvasHeight = TempHeight;
                    // Every time we rotate aspect ratio, toggle pageRotated - the image hasn't been transformed yet but the canvas has.
                    pageRotated = !pageRotated;
                    ScaleFactor = Math.Min(canvasWidth / (double)Extents.Width, canvasHeight / (double)Extents.Height);
                }
                else
                {
                    // Exit the loop if minimumScalingThreshold is hit.
                    break;
                }
            }
            FitLayersToEnvelope(new Envelope(0, 0, canvasWidth, canvasHeight));
        }

        /// <summary>
        /// Crop the layers collection to fit within a set of coordinates.
        /// </summary>
        /// <param name="minX">Most westerly coordinate.</param>
        /// <param name="minY">Most southerly coordinate.</param>
        /// <param name="maxX">Most Easterly coordinate.</param>
        /// <param name="maxY">Most northerly coordinate.</param>
        /// <param name="outputWidth">Width (in pixels) of output image.</param>
        /// <param name="outputHeight">Height (in pixels) of output image.</param>
        public void CropFeatures(double minX, double minY, double maxX, double maxY, int outputWidth, int outputHeight)
        {
            CropFeatures(new Envelope(minX, minY, maxX, maxY), outputWidth, outputHeight);
        }

        /// <summary>
        /// Crop the layers collection to fit within an envelope.
        /// </summary>
        /// <param name="viewport">Envelope to crop to.</param>
        /// <param name="outputWidth">Width (in pixels) of output image.</param>
        /// <param name="outputHeight">Height (in pixels) of output image.</param>
        public void CropFeatures(Envelope viewport, int outputWidth, int outputHeight)
        {
            // Fit viewport to page (ensure largest dimension fits, don't rotate).
            pageWidth = outputWidth;
            pageHeight = outputHeight;
            double ScaleFactor;
            if (viewport.Height > viewport.Width)
            {
                ScaleFactor = pageHeight / (double)viewport.Height;
            }
            else
            {
                ScaleFactor = pageWidth / (double)viewport.Width;
            }

            // resize canvas to appropriate size to fit viewport.
            Envelope OriginalExtents = Envelope.FindExtents(Layers);
            canvasWidth = Convert.ToInt32(OriginalExtents.Width * ScaleFactor);
            canvasHeight = Convert.ToInt32(OriginalExtents.Height * ScaleFactor);
            FitLayersToEnvelope(new Envelope(0, 0, canvasWidth, canvasHeight), rotate: false);

            // change origin to scaled origin of viewport.
            Envelope TranslatedExtents = Envelope.FindExtents(Layers);
            originX = Convert.ToInt32((viewport.MinX - -(TranslatedExtents.MinX / ScaleFactor - OriginalExtents.MinX)) * ScaleFactor);
            originY = Convert.ToInt32((viewport.MinY - -(TranslatedExtents.MinY / ScaleFactor - OriginalExtents.MinY)) * ScaleFactor);
            cropped = true;
        }

        private void FitLayersToEnvelope(Envelope envelope, bool? rotate = null)
        {
            Envelope LayersExtents = Envelope.FindExtents(Layers);
            for (int i = 0; i < Layers.Count; i++)
            {
                RotateAndScaleLayer(Layers[i], (int)envelope.Width, (int)envelope.Height, rotate: rotate, extents: LayersExtents);
            }
            LayersExtents = Envelope.FindExtents(Layers);
            //LayersExtents.Offset(-borderSize, -borderSize);
            for (int i = 0; i < Layers.Count; i++)
            {
                TranslateLayer(Layers[i], LayersExtents);
            }
            LayersExtents = Envelope.FindExtents(Layers);
        }

        /// <summary>
        /// Rotate, scale and translate the Layers collection to fit within the specified pixel dimensions.
        /// </summary>
        /// <param name="width">Desired width of output image in pixels, including border size (if any).</param>
        /// <param name="height">Desired height of output image in pixels, including border size (if any).</param>
        /// <param name="border">Size of border to add to output image.</param>
        public void FitLayersToPage(int width, int height, int border = 0)
        {
            borderSize = border;
            pageHeight = height - 2 * borderSize;
            pageWidth = width - 2 * borderSize;
            canvasHeight = height;
            canvasWidth = width;
            FitLayersToEnvelope(new Envelope(0, 0, pageWidth - borderSize * 2, pageHeight - borderSize * 2));
            multiPage = false;
            cropped = false;
        }


        /// <summary>
        /// Rotate and scale all features in a FeatureCollection to fit inside a box.
        /// </summary>
        /// <param name="features">FeatureCollection to rotate.</param>
        /// <param name="width">Width of bounding box.</param>
        /// <param name="height">Height of bounding box.</param>
        /// <param name="rotateRadians">Radians to rotate by. Defaults to 90 degrees.</param>
        /// <param name="rotate">Whether to rotate the features. If this is null, features will be rotated if input and output are different aspects.</param>
        /// <param name="extents">Extents to base size calculations on. Use to size groups of layers all together. If null, will use extents of "<paramref name="features"/>" parameter.</param>
        /// <returns></returns>
        public void RotateAndScaleLayer(Layer features, int width, int height, double rotateRadians = 4.7124, bool? rotate = null, Envelope extents = null)
        {
            Envelope Extents = extents ?? Envelope.FindExtents(features);

            double OutputAspect = width / (double)height;
            // If we're not sure whether to rotate, set rotate flag if one aspect > 1, but not both.
            bool Rotate = rotate ?? (Extents.AspectRatio > 1) ^ (OutputAspect > 1);
            if (Rotate == false) { rotateRadians = 0; }
            double ScaleFactor = Math.Min(width / (double)Extents.Width, height / (double)Extents.Height);

            for (int i = 0; i < features.Features.Count; i++)
            {
                Feature InFeature = features.Features[i];
                if (InFeature.Geometry != null)
                {
                    features.Features[i] = new Feature(RotateAndScaleGeometry(InFeature.Geometry, ScaleFactor, rotateRadians), InFeature.Properties, InFeature.Id != null ? InFeature.Id : null);
                }
            }
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
                        var Points = new List<GeoJSON.Net.Geometry.Point>();
                        for (int j = 0; j < MultiP.Coordinates.Count; j++)
                        {
                            var Position = MultiP.Coordinates[j].Coordinates;
                            if (rotateRadians != 0) { Position = RotatePositionAroundZero(Position, rotateRadians); }
                            Points.Add(new GeoJSON.Net.Geometry.Point(ScalePosition(Position, scaleFactor)));
                        }
                        return new MultiPoint(Points);
                    }
                case GeoJSONObjectType.LineString:
                    {
                        var Line = ((LineString)geometry);
                        var Positions = new List<IPosition>();
                        for (int j = 0; j < Line.Coordinates.Count; j++)
                        {
                            var P = Line.Coordinates[j];
                            if (rotateRadians != 0) { P = RotatePositionAroundZero(P, rotateRadians); }
                            Positions.Add(ScalePosition(P, scaleFactor));
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
                                var P = Line.Coordinates[j];
                                if (rotateRadians != 0) { P = RotatePositionAroundZero(P, rotateRadians); }
                                Positions.Add(ScalePosition(P, scaleFactor));
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
                                IPosition P = Line.Coordinates[j];
                                if (rotateRadians != 0) { P = RotatePositionAroundZero(P, rotateRadians); }
                                Positions.Add(ScalePosition(P, scaleFactor));
                            }
                            Lines.Add(new LineString(Positions));
                        }
                        return new Polygon(Lines);
                    }
                case GeoJSONObjectType.MultiPolygon:
                    var Polys = new List<Polygon>();
                    foreach (var Poly in ((MultiPolygon)geometry).Coordinates)
                    {
                        var Lines = new List<LineString>();
                        foreach (var Line in Poly.Coordinates)
                        {
                            var Positions = new List<IPosition>();
                            for (int j = 0; j < Line.Coordinates.Count; j++)
                            {
                                IPosition P = Line.Coordinates[j];
                                if (rotateRadians != 0) { P = RotatePositionAroundZero(P, rotateRadians); }
                                Positions.Add(ScalePosition(P, scaleFactor));
                            }
                            Lines.Add(new LineString(Positions));
                        }
                        Polys.Add(new Polygon(Lines));
                    }
                    return new MultiPolygon(Polys);
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


        private void TranslateLayers(List<Layer> layerCollection, double dx, double dy)
        {
            var Extents = Envelope.FindExtents(layerCollection);
            Extents.MinX -= dx;
            Extents.MinY -= dy;
            foreach (var Layer in layerCollection)
            {
                TranslateLayer(Layer, Extents);
            }
        }


        /// <summary>
        /// Iterate through feature collection and rebase its coordinates' origins to the minima of an Envelope.
        /// </summary>
        /// <param name="features">FeatureCollection to translate.</param>
        /// <param name="envelope">Envelope to use as origin for translation.</param>
        /// <returns>New FeatureCollection with coordinates of its component Features'Geometries rebased to the minima of an envelope.</returns>
        private void TranslateLayer(Layer features, Envelope envelope)
        {
            for (int f = 0; f < features.Features.Count; f++)
            {
                var Feature = features.Features[f];
                if (Feature.Geometry != null)
                {
                    features.Features[f] = new Feature(TranslateGeometry(Feature.Geometry, envelope), Feature.Properties, features.Features[f].Id != null ? features.Features[f].Id : null);
                }
            }
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
                        var Points = new List<GeoJSON.Net.Geometry.Point>();
                        for (int i = 0; i < MultiP.Coordinates.Count; i++)
                        {
                            Points.Add(new GeoJSON.Net.Geometry.Point(TranslatePosition(MultiP.Coordinates[i].Coordinates, envelope)));
                        }
                        return new MultiPoint(Points);
                    }
                case GeoJSONObjectType.LineString:
                    {
                        var Line = ((LineString)geometry);
                        var Positions = new List<IPosition>();
                        for (int i = 0; i < Line.Coordinates.Count; i++)
                        {
                            Positions.Add(TranslatePosition(Line.Coordinates[i], envelope));
                        }
                        return new LineString(Positions);
                    }
                case GeoJSONObjectType.MultiLineString:
                    {
                        var Lines = new List<LineString>();
                        foreach (var Line in ((MultiLineString)geometry).Coordinates)
                        {
                            var Positions = new List<IPosition>();
                            for (int i = 0; i < Line.Coordinates.Count; i++)
                            {
                                Positions.Add(TranslatePosition(Line.Coordinates[i], envelope));
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
                            for (int i = 0; i < Line.Coordinates.Count; i++)
                            {
                                Positions.Add(TranslatePosition(Line.Coordinates[i], envelope));
                            }
                            Lines.Add(new LineString( Positions));
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
                                var Positions = new List<IPosition>();
                                for (int i = 0; i < Line.Coordinates.Count; i++)
                                {
                                    Positions.Add(TranslatePosition(Line.Coordinates[i], envelope));
                                }
                                Lines.Add(new LineString(Positions));
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
            return new Position(coordinates.Latitude + (0 - (double)envelope.MinY), coordinates.Longitude + (0 - (double)envelope.MinX));
        }

        #endregion

        #region Rotate

        /// <summary>
        /// Rotate a position around the origin (0,0).
        /// </summary>
        /// <param name="position">IPosition object to rotate.</param>
        /// <param name="angle">Angle to rotate <paramref name="position"/> by, in radians.</param>
        /// <returns>New Position with the Latitude and Longitude rotated by <paramref name="angle"/> radians.</returns>
        public IPosition RotatePositionAroundZero(IPosition position, double angle)
        {
            var NewX = Math.Cos(angle) * position.Longitude - Math.Sin(angle) * position.Latitude;
            var NewY = Math.Sin(angle) * position.Longitude + Math.Cos(angle) * position.Latitude;
            return new Position(NewY, NewX, position.Altitude);
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
        private Bitmap RenderLayers()
        {
            var OutputBitmap = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format24bppRgb);
            drawingSurface = Graphics.FromImage(OutputBitmap);
            drawingSurface.FillRectangle(Brushes.White, new Rectangle(0, 0, OutputBitmap.Width, OutputBitmap.Height));
            drawingSurface.SmoothingMode = SmoothingMode.AntiAlias;

            foreach (var Layer in Layers)
            {
                foreach (var Item in Layer.Features)
                {
                    if (Item.Geometry != null)
                    {
                        DrawFeature(Item, Layer.Properties);
                    }
                }
            }
            return OutputBitmap;
        }

        private Bitmap RenderSegment(int xOffset, int yOffset, int width, int height)
        {
            int XSize = width;
            int YSize = height;
            int XOrigin = xOffset;
            int YOrigin = yOffset;

            // Create segment image
            var OutputBitmap = new Bitmap(pageWidth, pageHeight, PixelFormat.Format24bppRgb);
            drawingSurface = Graphics.FromImage(OutputBitmap);
            drawingSurface.FillRectangle(Brushes.White, new Rectangle(0, 0, pageWidth, pageHeight));
            drawingSurface.SmoothingMode = SmoothingMode.AntiAlias;

            // Segment size. Can't overflow original canvas without erroring so when we reach the end, just grab what's left.
            if (pageWidth - XOrigin + pageOverlap < width) { XSize = pageWidth - XOrigin + pageOverlap; }
            if (pageHeight - YOrigin + pageOverlap < height) { YSize = pageHeight - YOrigin + pageOverlap; }

            // Likewise, can't let origin go below 0.
            XOrigin = Math.Max(XOrigin - pageOverlap, 0);
            YOrigin = Math.Max(YOrigin - pageOverlap, 0);

            // Use to exclude features from being drawn if they're off the page.
            var PageExtents = new Envelope(0, 0, pageWidth, pageHeight);
            if (pageRotated)
            {
                PageExtents = new Envelope(originX, originY, pageHeight, pageWidth);
                int TempOrigin = XOrigin;
                XOrigin = YOrigin;
                YOrigin = TempOrigin;
            }

            // Duplicate layer collection and translate them to show the correct viewport.
            var RenderLayers = new List<Layer>(Layers.Select(l => (Layer)l.Clone()));
            TranslateLayers(RenderLayers, 0 - XOrigin, 0 - YOrigin);

            foreach (var Layer in RenderLayers)
            {
                foreach (var Feature in Layer.Features)
                {
                    if (Feature.Geometry != null && Clipper.GeometryIntersectsEnvelope(Feature.Geometry, PageExtents))
                    {
                        DrawFeature(Feature, Layer.Properties);
                    }
                }
            }
            return OutputBitmap;
        }


        /// <summary>
        /// Render the <see cref="Layers"/> collection to an image file.
        /// </summary>
        /// <param name="folderPath">Path to output folder.</param>
        /// <param name="filenameFormat">Format string for filenames. Include {0} for segments if paginated.</param>
        public bool SaveImage(string folderPath, string filenameFormat)
        {
            // Sanity check. Ensure path is a directory and that it exists.
            if (!Directory.Exists(folderPath)) { return false; }

            if (multiPage == true)
            {
                // Paginated to multiple pages.
                int XOffset = originX;
                int YOffset = originY;
                char YSegmentID = 'A';
                int XSegmentID = 0;

                int XSize = pageWidth - borderSize * 2;
                int YSize = pageHeight - borderSize * 2;

                var XSegments = ((canvasWidth / pageWidth) + (canvasWidth % pageWidth == 0 ? 0 : 1));
                var YSegments = ((canvasHeight / pageHeight) + (canvasHeight % pageHeight == 0 ? 0 : 1));


                for (int y = 1; y <= YSegments; y++)
                {
                    for (int x = 1; x <= XSegments; x++)
                    {
                        using (Bitmap Segment = RenderSegment(XOffset, YOffset, XSize, YSize))
                        {
                            string Filename = string.Format(filenameFormat, YSegmentID + XSegmentID.ToString());
                            // Add extension if it is missing.
                            if (Filename[Filename.Length - 4] != '.') { Filename += ".png"; }
                            Segment.Save(Path.Combine(folderPath, Filename));
                        }

                        XOffset += pageWidth - 2 * borderSize;
                        XSegmentID++;
                    }
                    XOffset = 0;
                    XSegmentID = 0;
                    YOffset += pageHeight - 2 * borderSize;
                    YSegmentID++;
                }
            }
            else if (cropped == true)
            {
                // Cropped down from initial map
                using (Bitmap Segment = RenderSegment(originX, originY, pageWidth, pageHeight))
                {
                    string Filename = string.Format(filenameFormat, "");
                    Segment.Save(Path.Combine(folderPath, Filename));
                }
            }
            else
            {
                // Single page
                using (var OutputBitmap = RenderLayers())
                {
                    string Filename = string.Format(filenameFormat, "");
                    OutputBitmap.Save(Path.Combine(folderPath, Filename));
                }
            }
            return true;
        }

        /// <summary>
        /// Render the <see cref="Layers"/> collection to an image file.
        /// </summary>
        /// <param name="path">Path to save output file.</param>
        public bool SaveImage(string path)
        {
            string FolderName = Path.GetDirectoryName(path);
            string FileName = Path.GetFileName(path);
            return SaveImage(FolderName, FileName);
        }

        /// <summary>
        /// Render the <see cref="Layers"/> collection to a single image in the form of a MemoryStream.
        /// </summary>
        /// <remarks>Paginating the layers may result in a much larger canvas than originally specified. </remarks>
        public MemoryStream ToStream()
        {
            using (var Canvas = RenderLayers())
            {
                var OutputStream = new MemoryStream();
                Canvas.Save(OutputStream, ImageFormat.Png);
                return OutputStream;
            }
        }

        /// <summary>
        /// Render the <see cref="Layers"/> collection to a collection of images.
        /// </summary>
        /// <returns></returns>
        public List<byte[]> ToList()
        {
            var Bitmaps = new List<byte[]>();
            var Converter = new ImageConverter();

            int XOffset = 0;
            int YOffset = 0;
            char YSegmentID = 'A';
            int XSegmentID = 0;

            int XSize = pageWidth - borderSize * 2;
            int YSize = pageHeight - borderSize * 2;

            while (YOffset < canvasHeight)
            {
                while (XOffset < canvasWidth)
                {
                    using (Bitmap Segment = RenderSegment(XOffset, YOffset, XSize, YSize))
                    {
                        Bitmaps.Add((byte[])Converter.ConvertTo(Segment, typeof(byte[])));
                    }

                    XOffset += pageWidth - 2 * borderSize;
                    XSegmentID++;
                }
                XOffset = 0;
                YOffset += pageHeight - 2 * borderSize;
                YSegmentID++;
            }

            return Bitmaps;
        }

        #endregion

        #region Drawing

        /// <summary>
        /// Take a feature and draw its geometry. This raises an event so the user can style the feature how they want.
        /// </summary>
        /// <param name="feature">Feature to draw.</param>
        /// <param name="layerProperties">Extended properties of layer.</param>
        private void DrawFeature(Feature feature, Dictionary<string, object> layerProperties)
        {
            var FeatureArguments = new DrawingFeatureEventArgs(feature, DefaultStyle, layerProperties);
            DrawingFeature?.Invoke(this, FeatureArguments);
            if (FeatureArguments.Cancel == false && feature.Geometry != null)
            {
                DrawGeometry(feature.Geometry, FeatureArguments.Style);
            }
        }

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
                    drawingSurface.DrawLine(style.LinePen, PtF, PtF);
                    break;
                case GeoJSONObjectType.MultiPoint:
                    foreach (var Points in ((MultiPoint)geometry).Coordinates) { DrawGeometry(Points, style); }
                    break;
                case GeoJSONObjectType.LineString:
                    drawingSurface.DrawLines(style.LinePen, ConvertPositionsToPoints(((LineString)geometry).Coordinates));
                    break;
                case GeoJSONObjectType.MultiLineString:
                    foreach (var Line in ((MultiLineString)geometry).Coordinates) { DrawGeometry(Line, style); }
                    break;
                case GeoJSONObjectType.Polygon:
                    var Path = new GraphicsPath();
                    foreach (var PolyLine in ((Polygon)geometry).Coordinates)
                    {
                        drawingSurface.DrawPolygon(style.LinePen, ConvertPositionsToPoints(PolyLine.Coordinates));
                        Path.AddPolygon(ConvertPositionsToPoints(PolyLine.Coordinates));
                    }
                    drawingSurface.DrawPath(style.LinePen, Path);

                    if (style.FillBrush != null) { drawingSurface.FillPath(style.FillBrush, Path); }

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
        private PointF[] ConvertPositionsToPoints(IList<IPosition> positions)
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
                    drawingSurface.Dispose();
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
