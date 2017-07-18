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
public GeoJsonRenderer(
    DrawingStyle defaultStyle = null,
    DrawingStyle optionalStyle = null
)
```
Both parameters are optional. The default style is a 2-pixel wide green line with no fill, and the optional style (see below) is a 2-pixel maroon outline around red-filled polygons.

### Examples
#### Single GeoJSON string -> image file
Rendering a single GeoJSON string to a file is straightforward:
```C#
var R = new GeoJsonRenderer();
var Reader = new StreamReader("testdata1.json");
var Json = Reader.ReadToEnd();
R.RenderGeoJson(Json, @"D:\TEMP\example1.png", 400, 300);
```
#### Multiple GeoJSON strings -> single image file
Rendering multiple strings to a file is not much more complex:
```C#
var R = new GeoJsonRenderer();
string[] Filenames = { "test-307-areas.json", "test-307-frame.json", "test-307-perimeter.json", "test-307-text.json" };
var Jsons = new List<string>();
foreach (var Name in Filenames)
{
	var Reader = new StreamReader(Name);
	string Text = Reader.ReadToEnd();
	Jsons.Add(Text);
	var Features = JsonConvert.DeserializeObject<FeatureCollection>(Text);		
}
R.RenderGeoJson(Jsons.ToArray(), @"D:\TEMP\example2.png", 640, 480);
```
#### Filter expressions
The optional `filterExpression` parameter allows us to show a particular subset of our GeoJSON:
```C#
var R = new GeoJsonRenderer();
var Reader = new StreamReader("testdata1.json");
var Json = Reader.ReadToEnd();
R.RenderGeoJson(Json, @"D:\TEMP\example3.png", 400, 300, (f => f.Properties["FLOOR"].ToString() == "1"));
``` 
FilterExpression will accept any method that can take a Feature and return a boolean.

#### Selective colour
GeoJsonRenderer's Optional Style is used to highlight Features based on an optional method parameter. Here we want to highlight our ground floor:
```C#
var R = new GeoJsonRenderer();
var Reader = new StreamReader("testdata1.json");
var Json = Reader.ReadToEnd();
R.RenderGeoJson(Json, @"D:\TEMP\example3.png", 400, 300, null, (f => f.Properties["FLOOR"].ToString() == "G"));
``` 
AlternativeStyleFunction will accept any method that can take a Feature and return a boolean.

We can also change the default and optional rendering styles:
```C#
var R = new GeoJsonRenderer();
R.OptionalStyle = new DrawingStyle(new Pen(Color.Blue, 5.0f), new SolidBrush(Color.DarkBlue));
```
Note that the FillBrush property of a DrawingStyle is only applied to Polygon Geometries - LineStrings that completely enclose an area will not be treated as polygons unless they are defined as such.

## Future developments
Note: these ideas may or may not be implemented.
+ Alternate output options (memorystream etc.)
+ Extend transform methods to be more useful for manipulating GeoJSON outside of a render-to-image context (arbitrary rotation centre etc.)
+ More comprehensive testing, unit tests and so on.
+ Make console app more fully-featured - currently configured through code, it'd be cool to see this made into a proper command-line tool rather than just the bare minimum to spit out a PNG file when I hit F5.