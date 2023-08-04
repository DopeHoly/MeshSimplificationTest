using g3;
using HelixToolkit.Wpf;
using SBRep;
using SBRep.Utils;
using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MeshSimplificationTest.SBRepVM
{
    public static class SbrepVizualizer
    {
        public static Model3D ModelFromEdge(SBRepObject mesh,
            IEnumerable<int> edgesIDs,
            Color color, double diameterScale = 0.5, double zZero = 0)
        {
            var theta = 4;
            var rad = diameterScale * 1.3 * 0.01;
            var phi = 4;
            var points = new List<Point3D>();
            var edges = new List<int>();
            Model3DGroup group = new Model3DGroup();
            var edgesColorized = new Dictionary<Color, List<int>>();
            foreach (var eid in edgesIDs)
            {
                var curentColor = color;
                if (!edgesColorized.ContainsKey(curentColor))
                    edgesColorized.Add(curentColor, new List<int>());
                var idx = mesh.Edges[eid].Vertices;
                var a = mesh.GetVertex(idx.a);
                var b = mesh.GetVertex(idx.b);
                var ad = new Point3D(a.x, a.y, zZero);
                var bd = new Point3D(b.x, b.y, zZero);
                if (!points.Contains(ad))
                {
                    points.Add(ad);
                }
                if (!points.Contains(bd))
                {
                    points.Add(bd);
                }
                edgesColorized[curentColor].Add(points.FindIndex(x => x.Equals(ad)));
                edgesColorized[curentColor].Add(points.FindIndex(x => x.Equals(bd)));
            }

            foreach (var colorEdges in edgesColorized)
            {
                var builderEdges = new MeshBuilder(true, true);
                builderEdges.AddEdges(points, colorEdges.Value, 0.01 * diameterScale, theta);
                group.Children.Add(new GeometryModel3D()
                {
                    Geometry = builderEdges.ToMesh(),
                    Material = new DiffuseMaterial(new SolidColorBrush(colorEdges.Key))
                });
            }

            var builder = new MeshBuilder(true, true);
            //builder.AddEdges(points, edges, 0.01 * diameterScale, theta);
            foreach (var point in points)
                builder.AddEllipsoid(point, rad, rad, rad, theta, phi);
            group.Children.Add(new GeometryModel3D()
            {
                Geometry = builder.ToMesh(),
                Material = new DiffuseMaterial(new SolidColorBrush(color))
            });
            return group;
        }

        public static Model3D GenerateModelFrom2dContour(IEnumerable<Vector2d> contour, Color color, double contsZ = 0, double diameterScale = 1)
        {
            var theta = 4;
            var rad = diameterScale * 1.3 * 0.01;
            var phi = 4;
            var builder = new MeshBuilder(true, true);
            var points = new List<Point3D>();
            var edges = new List<int>();
            foreach (var vtx in contour)
            {
                var a = vtx;
                var point = new Point3D(a.x, a.y, contsZ);
                points.Add(point);
            }
            for (int i = 0; i < points.Count; ++i)
            {
                var a = i;
                var b = i + 1;
                if (b == points.Count)
                    b = 0;
                edges.Add(a);
                edges.Add(b);
            }
            builder.AddEdges(points, edges, 0.01 * diameterScale, theta);
            foreach (var point in points)
                builder.AddEllipsoid(point, rad, rad, rad, theta, phi);
            return new GeometryModel3D()
            {
                Geometry = builder.ToMesh(),
                Material = new DiffuseMaterial(new SolidColorBrush(color))
            };
        }

        public static void ShowEdge(SBRepObject sbrep, Dictionary<Color, IEnumerable<int>> colorsToEdge, IntersectContour contour = null)
        {
            var resultmodels = new Model3DGroup();
            foreach (var ctoedges in colorsToEdge)
            {
                var color = ctoedges.Key;
                resultmodels.Children.Add(
                    ModelFromEdge(
                        sbrep,
                        ctoedges.Value,
                        color,
                         0.1));
            }

            if(contour != null)
            {
                resultmodels.Children.Add(GenerateModelFrom2dContour(contour.GetContourPoints(), Colors.Aquamarine, diameterScale: 0.1));
            }

            var sbrepVisualizer = new SbrepVisualizerViewer();
            sbrepVisualizer.Model = resultmodels;
            _ = sbrepVisualizer.ShowDialog();
        }

        public static void ShowEdgePlot(SBRepObject sbrep, Dictionary<Color, IEnumerable<int>> colorsToEdge, IntersectContour contour = null)
        {

            var plotModel = new PlotModel();

            try
            {
                foreach (var ctoedges in colorsToEdge)
                {
                    var color = ctoedges.Key;
                    foreach (var eid in ctoedges.Value)
                    {
                        var plotSeries = new LineSeries()
                        {
                            MarkerType = OxyPlot.MarkerType.Circle,
                            LineStyle = OxyPlot.LineStyle.Solid,
                            Color = OxyColor.FromArgb(color.A, color.R, color.G, color.B)
                        };
                        var coords = sbrep.GetEdgeCoordinates(eid);
                        plotSeries.Points.Add(new OxyPlot.DataPoint(coords.Item1.x, coords.Item1.y));
                        plotSeries.Points.Add(new OxyPlot.DataPoint(coords.Item2.x, coords.Item2.y));
                        plotModel.Series.Add(plotSeries);
                    }
                }
            }
            catch(Exception ex)
            {
                ;
            }
            

            if (contour != null)
            {
                var color = Colors.Blue;
                var plotSeries = new LineSeries()
                {
                    MarkerType = OxyPlot.MarkerType.Circle,
                    LineStyle = OxyPlot.LineStyle.Solid,
                    Color = OxyColor.FromArgb(color.A, color.R, color.G, color.B)
                };
                var points = contour.GetContourPoints();
                foreach (var item in points)
                {
                    plotSeries.Points.Add(new OxyPlot.DataPoint(item.x, item.y));
                }
                plotSeries.Points.Add(new OxyPlot.DataPoint(points.First().x, points.First().y));
                plotModel.Series.Add(plotSeries);
            }

            var vizualizer = new OxyplotVizualizer();
            vizualizer.Model = plotModel;
            vizualizer.OnPropertyChanged("Model");
            _ = vizualizer.ShowDialog();
        }

        public static void ShowContours(IEnumerable<IntersectContour> contours)
        {
            var plotModel = new PlotModel();

            foreach (var contour in contours)
            {
                if (contour != null)
                {
                    var plotSeries = new LineSeries()
                    {
                        MarkerType = OxyPlot.MarkerType.Circle,
                        LineStyle = OxyPlot.LineStyle.Solid,
                    };
                    var points = contour.GetContourPoints();
                    foreach (var item in points)
                    {
                        plotSeries.Points.Add(new OxyPlot.DataPoint(item.x, item.y));
                    }
                    plotSeries.Points.Add(new OxyPlot.DataPoint(points.First().x, points.First().y));
                    plotModel.Series.Add(plotSeries);
                }
            }

            var vizualizer = new OxyplotVizualizer();
            vizualizer.Model = plotModel;
            vizualizer.OnPropertyChanged("Model");
            _ = vizualizer.ShowDialog();
        }

        public static void ShowEdges(IEnumerable<Vector2d> vertices, IEnumerable<Index2i> edges)
        {
            var plotModel = new PlotModel();

            foreach (var edge in edges)
            {
                var plotSeries = new LineSeries()
                {
                    MarkerType = OxyPlot.MarkerType.Circle,
                    LineStyle = OxyPlot.LineStyle.Solid,
                };
                if (edge.a >= vertices.Count() || edge.b >= vertices.Count())
                  throw new ArgumentOutOfRangeException();
                
                var points = new List<Vector2d>() { vertices.ElementAt(edge.a), vertices.ElementAt(edge.b) };
                foreach (var item in points)
                {
                    plotSeries.Points.Add(new OxyPlot.DataPoint(item.x, item.y));
                }
                //plotSeries.Points.Add(new OxyPlot.DataPoint(points.First().x, points.First().y));
                plotModel.Series.Add(plotSeries);
            }

            var vizualizer = new OxyplotVizualizer();
            vizualizer.Model = plotModel;
            vizualizer.OnPropertyChanged("Model");
            _ = vizualizer.ShowDialog();
        }

        public static void ShowTriangles(IEnumerable<Vector2d> vertices, IEnumerable<Index3i> triangles)
        {
            var plotModel = new PlotModel();

            foreach (var tri in triangles)
            {
                var plotSeries = new LineSeries()
                {
                    MarkerType = OxyPlot.MarkerType.Circle,
                    LineStyle = OxyPlot.LineStyle.Solid,
                };
                if (tri.a >= vertices.Count() || tri.b >= vertices.Count() || tri.c >= vertices.Count())
                    throw new ArgumentOutOfRangeException();

                var points = new List<Vector2d>() 
                { 
                    vertices.ElementAt(tri.a), 
                    vertices.ElementAt(tri.b), 
                    vertices.ElementAt(tri.c), 
                    vertices.ElementAt(tri.a) 
                };
                foreach (var item in points)
                {
                    plotSeries.Points.Add(new OxyPlot.DataPoint(item.x, item.y));
                }
                //plotSeries.Points.Add(new OxyPlot.DataPoint(points.First().x, points.First().y));
                plotModel.Series.Add(plotSeries);
            }

            var vizualizer = new OxyplotVizualizer();
            vizualizer.Model = plotModel;
            vizualizer.OnPropertyChanged("Model");
            _ = vizualizer.ShowDialog();
        }
    }
}
