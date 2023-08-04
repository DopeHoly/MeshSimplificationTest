using g3;
using HelixToolkit.Wpf;
using MeshSimplificationTest.SBRepVM;
using SBRep.Utils;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MeshSimplificationTest
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //DataContext = new RemeshViewModel();
            //DataContext = new MasterViewModel();
            DataContext = new SBRepViewModel();

            //var contour = new List<Vector2d>()
            //{
            //    new Vector2d(0, 0),
            //    new Vector2d(4, 0),
            //    new Vector2d(4, 4),
            //    new Vector2d(0, 4),
            //};

            //SbrepVizualizer.FuncMonteCarloTest(
            //    (i, j) => Geometry2DHelper.InContour(contour, new Vector2d(i, j)) ? 100 : 0);

            //SbrepVizualizer.FuncMonteCarloTest(
            //    (i, j) => Geometry2DHelper.EqualPoints(Vector2d.Zero, new Vector2d(i, j), 1) ? 100 : 0);

            //Geometry2DHelper.IsPointOnLineSegment(new Vector2d(0, 0.5), Vector2d.Zero, new Vector2d(1, 0), 0.5);

            //SbrepVizualizer.FuncMonteCarloTest(
            //    (i, j) =>
            //    {
            //        var pt = new Vector2d(i, j);
            //        var a = new Vector2d(1, 1);
            //        var b = new Vector2d(2, 2);
            //        var c = new Vector2d(0, 2);
            //        var d = new Vector2d(1, 3);
            //        return Geometry2DHelper.PointInRectangle(pt, a, b, c, d) ? 0 : 100;
            //    });

            //SbrepVizualizer.FuncMonteCarloTest(
            //    (i, j) =>
            //    {
            //        var pt = new Vector2d(i, j);
            //        var l1 = new Vector2d(1, 3);
            //        var l2 = new Vector2d(3, 1);
            //        return Math.Abs((pt.x - l1.x) * (l2.y - l1.y) - (pt.y - l1.y) * (l2.x - l1.x)) -
            //        Geometry2DHelper.PointToLineDistance(pt, l1, l2);
            //    });

            //SbrepVizualizer.FuncMonteCarloTest(
            //    (i, j) => Geometry2DHelper.IsPointOnLineSegment(new Vector2d(i, j), Vector2d.Zero, new Vector2d(3, 3), 0.1) ? 100 : 0);

            //SbrepVizualizer.FuncMonteCarloTest(
            //    (i, j) => Geometry2DHelper.IsPointOnLineSegment(new Vector2d(i, j), new Vector2d(1, 1), new Vector2d(3, 1), 0.5) ? 100 : 0);
            //SbrepVizualizer.FuncMonteCarloTest(
            //    (i, j) => Geometry2DHelper.IsPointOnLineSegment(new Vector2d(i, j), new Vector2d(1, 1), new Vector2d(1, 3), 0.5) ? 100 : 0);

            //var value = Geometry2DHelper.PointToLineDistance(Vector2d.Zero, Vector2d.Zero, new Vector2d(1, 1));
            //value = Geometry2DHelper.PointToLineDistance(Vector2d.Zero, new Vector2d(3, 3), new Vector2d(1, 1));
            //value = Geometry2DHelper.PointToLineDistance(Vector2d.Zero, new Vector2d(1, 1), new Vector2d(1, 1));

            //SbrepVizualizer.FuncMonteCarloTest(
            //    (i, j) => Geometry2DHelper.IsPointOnLineSegment(new Vector2d(i, j), new Vector2d(1, 3), new Vector2d(3, 1), 1) ? 100 : 0);

            //SbrepVizualizer.FuncMonteCarloTest(
            //    (i, j) => Geometry2DHelper.IsPointOnLineSegment(new Vector2d(i, j), new Vector2d(1, 1), new Vector2d(1, 5), 0) ? 100 : 0);
        }
    }
}
