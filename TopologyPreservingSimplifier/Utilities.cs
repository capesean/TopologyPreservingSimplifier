using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Linemerge;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Precision;
using System.Collections.Generic;
using System.Linq;

namespace Utilities
{
    public static class Geo
    {
        public static Dictionary<string, IGeometry> SimplifyAndReduce(
            Dictionary<string, IGeometry> shapes,
            double tolerance = 1e-2,
            double precision = 1e4)
        {
            // extract the boundaries
            var boundaries = GetBoundaries(shapes);

            // union the boundaries to for a single geometry
            var unionedBoundaries = Union(boundaries);

            // merge the lines
            var mergedLineStrings = MergeLines(unionedBoundaries);

            // simplify
            var simplified = Simplify(mergedLineStrings, tolerance);

            // recreate polygons 
            var newPolygons = Polygonize(simplified);

            // reduce precision
            var reduced = Reduce(newPolygons, precision);

            // match back to id
            var matched = Match(reduced, shapes);

            // make valid (right hand rule)
            var results = MakeValid(matched);

            return results;
        }

        public static IEnumerable<IGeometry> GetBoundaries(Dictionary<string, IGeometry> shapes)
        {
            // note: this means interior rings are not handled
            // could convert to polygons and then use .Shell?
            return shapes.Select(o => o.Value.Boundary).ToList();
        }

        public static IGeometry Union(IEnumerable<IGeometry> shapes)
        {
            return new NetTopologySuite.Operation.Union.UnaryUnionOp(shapes).Union();
        }

        public static IList<IGeometry> MergeLines(IGeometry geometry)
        {
            var lineMerger = new LineMerger();
            lineMerger.Add(geometry);
            return lineMerger.GetMergedLineStrings();
        }

        public static IGeometry Simplify(IEnumerable<IGeometry> geometries, double tolerance)
        {
            var geometryCollection = new GeometryCollection(geometries.ToArray());
            return NetTopologySuite.Simplify.TopologyPreservingSimplifier.Simplify(geometryCollection, tolerance);
        }

        public static ICollection<IGeometry> Polygonize(IGeometry geometry)
        {
            var polygonizer = new Polygonizer(false);
            polygonizer.Add(geometry);
            return polygonizer.GetPolygons();
        }

        public static IEnumerable<IGeometry> Reduce(ICollection<IGeometry> geometries, double precision)
        {
            var reducer = new GeometryPrecisionReducer(new PrecisionModel(precision));
            return geometries.Select(o => reducer.Reduce(o));
        }

        public static Dictionary<string, IGeometry> Match(IEnumerable<IGeometry> geometries, Dictionary<string, IGeometry> shapes)
        {
            var matched = new Dictionary<string, IGeometry>();
            foreach (Polygon geometry in geometries)
            {
                foreach (var shape in shapes)
                {
                    if (!shape.Value.Intersects(geometry)) continue;
                    if (shape.Value.Intersection(geometry).Area / geometry.Area > 0.5
                        )
                    {
                        if (matched.ContainsKey(shape.Key))
                            matched[shape.Key] = matched[shape.Key].Union(geometry);
                        else
                            matched.Add(shape.Key, geometry);
                        break;
                    }
                }
            }
            return matched;
        }

        public static Dictionary<string, IGeometry> MakeValid(Dictionary<string, IGeometry> shapes)
        {
            var results = new Dictionary<string, IGeometry>();
            foreach (var shape in shapes)
            {
                if (shape.Value.GeometryType == "MultiPolygon")
                {
                    var multi = (MultiPolygon)shape.Value;

                    var polygons = new List<Polygon>();
                    for (var i = 0; i < multi.NumGeometries; i++)
                    {
                        var poly = (Polygon)multi.Geometries[i];
                        if (!poly.Shell.IsCCW)
                            polygons.Add((Polygon)poly.Reverse());
                        else
                            polygons.Add(poly);
                    }
                    results.Add(shape.Key, new MultiPolygon(polygons.ToArray()));
                }
                else
                {
                    results.Add(shape.Key, ((Polygon)shape.Value).Shell.IsCCW ? shape.Value : shape.Value.Reverse());
                }
            }
            return results;
        }
    }
}
