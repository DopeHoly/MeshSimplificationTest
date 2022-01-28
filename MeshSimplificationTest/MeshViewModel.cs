using g3;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MeshSimplificationTest
{

    public class MeshViewModel : INotifyPropertyChanged
    {
        private bool _visible;
        private DMesh3 _model;
        public DMesh3 Model
        {
            get => _model;
            set
            {
                if (_model == value) return;
                _model = value;
                Model_3D = ConvertToModel3D(_model);
                Warframe = GetWarframe();
                OnPropertyChanged(nameof(Model));
                //OnPropertyChanged(nameof(Model_3D));
                //OnPropertyChanged(nameof(Warframe));
            }
        }
        public Model3D Model_3D { get; set; }
        public Model3D Warframe { get; set; }
        public string Name { get; set; }
        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                OnPropertyChanged();
            }
        }

        public void ChangeColor(Color color)
        {
            //Model_3D = new GeometryModel3D()
            //{
            //    Geometry = (Model_3D as GeometryModel3D).Geometry,
            //    Material = new DiffuseMaterial(new SolidColorBrush(color))
            //};
        }

        public Model3D GetWarframe()
        {
            if (Model_3D is Model3DGroup)
            {
                var wireframe = new Model3DGroup();
                var models = Model_3D as Model3DGroup;
                foreach (GeometryModel3D model in models.Children)
                {
                    var triangleMesh = MeshExtensions.ToWireframe(
                            model.Geometry as MeshGeometry3D,
                            0.005);
                    DiffuseMaterial wireframe_material =
                        new DiffuseMaterial(Brushes.Black);
                    var frame = new GeometryModel3D()
                    {
                        Geometry = triangleMesh,
                        Material = new DiffuseMaterial(Brushes.Black)
                    };
                    wireframe.Children.Add(frame);
                }
                return wireframe;
            }
            if (Model_3D is GeometryModel3D)
            {
                var triangleMesh = MeshExtensions.ToWireframe(
                            (Model_3D as GeometryModel3D).Geometry as MeshGeometry3D,
                            0.005);
                DiffuseMaterial wireframe_material =
                    new DiffuseMaterial(Brushes.Black);
                var wireframe = new GeometryModel3D()
                {
                    Geometry = triangleMesh,
                    Material = new DiffuseMaterial(Brushes.Black)
                };
                return wireframe;
            }
            return null;
        }

        public Model3D GetModel(Color color)
        {
            var mesh = GetMesh3D();
            var geometry = mesh.ToMeshGeometry3D();
            return new GeometryModel3D()
            {
                Geometry = geometry,
                Material = new DiffuseMaterial(new SolidColorBrush(color))
            };
        }

        public Mesh3D GetMesh3D()
        {
            var points = new List<Point3D>(Model.VertexCount);
            var triangleIndex = new List<int>(Model.TriangleCount * 3);

            foreach (var vector in Model.Vertices())
            {
                points.Add(new Point3D(vector.x, vector.y, vector.z));
            }

            foreach (int triangleid in Model.TriangleIndices())
            {
                var triangle = Model.GetTriangle(triangleid);
                if (triangle.a >= points.Count ||
                     triangle.b >= points.Count ||
                     triangle.c >= points.Count)
                {
                    continue;
                }
                triangleIndex.Add(triangle.a);
                triangleIndex.Add(triangle.b);
                triangleIndex.Add(triangle.c);
            }
            return new Mesh3D(points, triangleIndex);
        }

        private void SaveTriangleMeshByIdInFile(DMesh3 model, int[] tri_list, string bufer_group_PATH, WriteOptions writeOptions)
        {
            var outputMesh = new DMesh3();
            var vtxCounter = 0;
            var gr = model.GetTriangleGroup(1);
            Dictionary<int, int> vertex = new Dictionary<int, int>();
            foreach (var trianglID in tri_list)
            {
                var triVtx = model.GetTriangle(trianglID);
                int[] trivtx = { triVtx.a, triVtx.b, triVtx.c };
                foreach (var vtxID in trivtx)
                {
                    if (!vertex.ContainsKey(vtxID))
                    {
                        vertex.Add(vtxID, vtxCounter);
                        ++vtxCounter;
                    }
                }
            }
            foreach (var item in vertex)
            {
                outputMesh.AppendVertex(model.GetVertex(item.Key));
            }
            foreach (var trianglID in tri_list)
            {
                var triVtx = model.GetTriangle(trianglID);
                outputMesh.AppendTriangle(vertex[triVtx.a], vertex[triVtx.b], vertex[triVtx.c]);
            }
            StandardMeshWriter.WriteFile(
                bufer_group_PATH,
                new List<WriteMesh>() { new WriteMesh(outputMesh) },
                writeOptions);
        }
        private Model3DGroup LoadTriangleMesh(string bufer_PATH, int idGroup = -1)
        {
            ModelImporter import = new ModelImporter();
            var models = import.Load(bufer_PATH);
            var resultmodels = new Model3DGroup();
            foreach (GeometryModel3D geometryModel in models.Children)
            {
                var mesh = geometryModel.Geometry as MeshGeometry3D;
                var resultMesh = new GeometryModel3D()
                {
                    Geometry = mesh,
                    Material = new DiffuseMaterial(new SolidColorBrush(
                         GetColor(idGroup)))
                };

                //var triangleMesh = MeshExtensions.ToWireframe(mesh, 0.005);
                //DiffuseMaterial wireframe_material =
                //    new DiffuseMaterial(Brushes.Black);
                //var wireframe = new GeometryModel3D()
                //{
                //    Geometry = triangleMesh,
                //    Material = new DiffuseMaterial(Brushes.Black)
                //};
                //var mat = new MaterialGroup();
                //mat.Children.Add(new DiffuseMaterial(Brushes.Aqua));
                //mat.Children.Add(new SpecularMaterial(Brushes.Blue, 80));

                resultmodels.Children.Add(resultMesh);
                //resultmodels.Children.Add(wireframe);
            }
            return resultmodels;
        }

        private static Dictionary<int, Color> groupColors = new Dictionary<int, Color>();


        private Color GetColor(int id)
        {
            if (groupColors.ContainsKey(id)) return groupColors[id];
            groupColors.Add(id, GetRandomColor());
            return groupColors[id];
        }
        private Random rnd = new Random();
        private Color GetRandomColor(int id = -1)
        {
            return Color.FromRgb(
                            (byte)rnd.Next(256),
                            (byte)rnd.Next(256),
                            (byte)rnd.Next(256));
        }

        private string MODEL_PATH = Path.Combine(Directory.GetCurrentDirectory(), "cube.stl");
        private string bufer_PATH = Path.Combine(Directory.GetCurrentDirectory(), @"bufer.obj");
        private string bufer_group_PATH = Path.Combine(Directory.GetCurrentDirectory(), @"buferGrTMP.obj");
        private Model3D ConvertToModel3D(DMesh3 model)
        {
            Model3DGroup resultmodels = null;
            try
            {
                WriteOptions writeOptions = new WriteOptions();
                writeOptions.bPerVertexColors = true;

                int[][] group_tri_sets = FaceGroupUtil.FindTriangleSetsByGroup(model);
                if (group_tri_sets.Length > 0)
                {
                    resultmodels = new Model3DGroup();
                    var cnt = 0;
                    foreach (int[] tri_list in group_tri_sets)
                    {
                        SaveTriangleMeshByIdInFile(model, tri_list, bufer_group_PATH, writeOptions);
                        resultmodels.Children.Add(LoadTriangleMesh(bufer_group_PATH, cnt));
                        ++cnt;
                    }
                }
                else
                {
                    StandardMeshWriter.WriteFile(
                        bufer_PATH,
                        new List<WriteMesh>() { new WriteMesh(model) },
                        writeOptions);
                    resultmodels = LoadTriangleMesh(bufer_PATH);
                }

                //Import 3D model file


            }
            catch (Exception e)
            {
                // Handle exception in case can not file 3D model
                MessageBox.Show("Exception Error : " + e.StackTrace);
            }
            return resultmodels;
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, args);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
