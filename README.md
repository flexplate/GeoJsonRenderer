# GeoJsonRenderer - Convert GeoJSON to images

Convert a GeoJSON string (or multiple GeoJSON strings) to PNG images.

This package currently contains 2 Visual Studio 2015 projects:
+ GeoJsonRenderer, the class library that converts GeoJSON text to PNG images.
+ GeoJsonConsole, a (very) simple command-line utility used to test GeoJsonRenderer during development.

## Usage
### Dependencies
In order to use GeoJsonRenderer in your projects, you will need to reference the following packages:
+ Json.NET (https://www.nuget.org/packages/Newtonsoft.Json/)
+ GeoJSON.Net (https://www.nuget.org/packages/GeoJSON.Net/)

### Construction
Before using the renderer, it must be instantiated:
```C#
var Renderer = new GeoJsonRenderer(DefaultStyle);
```
The styling parameter is optional. The default style is a 2-pixel wide green line with no fill.

### Styling
We can also change the default rendering style by instantiating a DrawingStyle object. The constructor takes the form `new DrawingStyle(System.Drawing.Pen LinePen, System.Drawing.Brush FillBrush)`.
```C#
var R = new GeoJsonRenderer();
R.DefaultStyle = new DrawingStyle(new Pen(Color.Blue, 5.0f), new SolidBrush(Color.DarkBlue));
```
Note that the FillBrush property of a DrawingStyle is only applied to Polygon Geometries - LineStrings that completely enclose an area will not be treated as polygons unless they are defined as such.


### Examples
#### Single GeoJSON string -> image file
Rendering a single GeoJSON string to a file is straightforward:
```C#
using(var R = new GeoJsonRenderer())
{
    var Reader = new StreamReader("testdata1.json");
    var Json = Reader.ReadToEnd();
    R.LoadGeoJson(Json);
    R.FitLayersToPage(640, 480);
    R.SaveImage(@"D:\TEMP\example1.png");
}
```
#### Multiple GeoJSON strings -> single image file
Rendering multiple strings to a file is not much more complex:
```C#
string[] Filenames = { "test-areas.json", "test-frame.json", "test-perimeter.json", "test-text.json" };
string[] Jsons = new string[Filenames.Length];
foreach (var Name in Filenames)
{
    var Reader = new StreamReader(Filenames[i]);
    string Text = Reader.ReadToEnd();
    Jsons[i] = Text;
}
using(var R = new GeoJsonRenderer())
{
    R.LoadGeoJson(Jsons);
    R.FitLayersToPage(640, 480);
    R.SaveImage(@"D:\TEMP\example2.png");
}
```
#### Already deserialised?
If your GeoJson objects are already deserialised (perhaps you're doing some other processing on them outside of just rendering to an image), you can add them directly:
```C#
FeatureCollection Features = JsonConvert.DeserializeObject<FeatureCollection>(json);
// (Your processing code here)
using(var R = new GeoJsonRenderer())
{
    R.Layers.Add(Features);
    R.FitLayersToPage(640, 480);
    R.SaveImage(@"D:\TEMP\example3.png");
}
```
#### Selective colour
GeoJsonRenderer raises an event before drawing each feature. We can handle that event to style features based on their properties. Here we want to highlight our ground floor:
##### Main():
```C#
var reader = new StreamReader("testdata1.json");
var Json = reader.ReadToEnd();
using(var R = new GeoJsonRenderer())
{
    R.DrawingFeature += R_DrawingFeature;
    R.LoadGeoJson(Json);
    R.FitLayersToPage(640, 480);
    R.SaveImage(@"D:\TEMP\example4.png");
}
``` 
##### R_DrawingFeature():
```C#
var OptionalStyle = new DrawingStyle(new Pen(Color.Blue, 5.0f), new SolidBrush(Color.DarkBlue));
if (e.Feature.Properties.ContainsKey("FLOOR") && e.Feature.Properties["FLOOR"].ToString() == "1")
{
	e.Style = OptionalStyle;
}
```

#### Adding a margin
The `FitLayersToPage` method accepts an optional parameter to add a margin (hence, keeping content from going right to the edge of the rendered image) as follows:
```C#
using(var R = new GeoJsonRenderer())
{
    R.LoadGeoJson(Json);
    R.FitLayersToPage(640, 480, 20);    // 20-pixel margin
    R.SaveImage(@"D:\TEMP\example5.png");
}
```

#### Splitting an image across multiple pages
The Paginate method can be used instead of `FitLayersToPage`. It accepts a threshold value for scaling. If the scale factor is below this threshold the canvas size is recursively doubled until the scale factor is above the threshold. In this circumstance, `SaveImage(path)` must be given a folder path not a file.
```C#
using(var R = new GeoJsonRenderer())
{
    R.LoadGeoJson(Json);
    R.Paginate(200, 100, 0.5, 20);  // 20-pixel margin on each page
    R.SaveImage(@"D:\TEMP\example6");
}
```

## Future developments
Note: these ideas may or may not be implemented.
+ Extend transform methods to be more useful for manipulating GeoJSON outside of a render-to-image context (arbitrary rotation centre etc.)
+ More comprehensive testing, unit tests and so on.
+ Make console app more fully-featured - currently configured through code, it'd be cool to see this made into a proper command-line tool rather than just the bare minimum to spit out a PNG file when I hit F5.
