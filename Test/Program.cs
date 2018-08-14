using GeoAPI.Geometries;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Test
{
    class Program
    {
        private static string OutputFolder = @"C:\Users\seanm\Desktop\districts\";

        public static void Main(string[] args)
        {
            var tolerance = 0.01d;
            var precision = 1000d;
            var watch = new Stopwatch();

            // load shapes
            watch.Restart();
            var shapes = LoadShapeFile(@"C:\Users\seanm\Desktop\districts\DistrictMunicipalities2011.shp", "MUNICNAME");
            Log(shapes, "1-shapes", watch);

            // extract the boundaries
            watch.Restart();
            var boundaries = Utilities.Geo.GetBoundaries(shapes);
            Log(boundaries, "2-boundaries", watch);

            // union the boundaries to for a single geometry
            watch.Restart();
            var unionedBoundaries = Utilities.Geo.Union(boundaries);
            Log(unionedBoundaries, "3-unionedBoundaries", watch);

            // merge the lines
            watch.Restart();
            var mergedLineStrings = Utilities.Geo.MergeLines(unionedBoundaries);
            Log(mergedLineStrings, "4-mergedLineStrings", watch);

            // simplify
            watch.Restart();
            var simplified = Utilities.Geo.Simplify(mergedLineStrings, tolerance);
            Log(simplified, "5-simplified", watch);

            // recreate polygons 
            watch.Restart();
            var newPolygons = Utilities.Geo.Polygonize(simplified);
            Log(newPolygons, "6-newPolygons", watch);

            // reduce precision
            watch.Restart();
            var reduced = Utilities.Geo.Reduce(newPolygons, precision);
            Log(reduced, "7-reduced", watch);

            // match back to id
            watch.Restart();
            var matched = Utilities.Geo.Match(reduced, shapes);
            Log(matched, "8-matched", watch);

            // make valid (right hand rule)
            watch.Restart();
            var valid = Utilities.Geo.MakeValid(matched);
            Log(valid, "9-valid", watch);

            Console.WriteLine("---FINISHED---");
            Console.ReadKey();
        }

        private static void Log(Dictionary<string, IGeometry> shapes, string step, Stopwatch watch)
        {
            watch.Stop();
            Console.WriteLine(step + $": {watch.ElapsedMilliseconds / 1000m} seconds");

            if (OutputFolder == null) return;

            var geoJson = new GeoJsonWriter();
            var features = new FeatureCollection();
            foreach (var result in shapes)
            {
                var feature = new Feature();
                feature.Geometry = result.Value;
                var attributesTable = new AttributesTable();
                if (result.Key != null)
                {
                    attributesTable.Add("Id", result.Key);
                }
                feature.Attributes = attributesTable;
                features.Add(feature);
            }
            var json = geoJson.Write(features);
            File.WriteAllText(OutputFolder + $@"{step}.json", json);
        }

        private static void Log(IGeometry geometry, string step, Stopwatch watch)
        {
            Log(new List<IGeometry> { geometry }, step, watch);
        }

        private static void Log(IEnumerable<IGeometry> geometries, string step, Stopwatch watch)
        {
            var shapes = new Dictionary<string, IGeometry>();
            var i = 0;
            foreach (var geometry in geometries)
                shapes.Add((i++).ToString(), geometry);
            Log(shapes, step, watch);
        }

        private static Dictionary<string, IGeometry> LoadShapeFile(string shapefileName, string keyFieldName)
        {
            var shapes = new Dictionary<string, IGeometry>();

            var factory = new GeometryFactory();
            using (var shapeFileDataReader = new ShapefileDataReader(shapefileName, factory))
            {
                var header = shapeFileDataReader.DbaseHeader;

                while (shapeFileDataReader.Read())
                {
                    for (var i = 0; i < header.NumFields; i++)
                    {
                        if (header.Fields[i].Name == keyFieldName)
                        {
                            var key = Convert.ToString(shapeFileDataReader.GetValue(i));
                            var geometry = (Geometry)shapeFileDataReader.Geometry;
                            shapes.Add(key, geometry);
                        }
                    }
                }
            }

            return shapes;
        }

    }
}
