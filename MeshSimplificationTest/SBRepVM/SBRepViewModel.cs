using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static gs.MeshSpatialSort;
using System.Windows.Interop;
using System.Windows.Media.Media3D;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows;
using System.IO;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Markup;
using MeshSimplificationTest.SBRep;
using CGALDotNet.Polyhedra;
using static MeshSimplificationTest.SBRep.SBRepBuilder;
using Parabox.CSG;
using Net3dBool;

namespace MeshSimplificationTest.SBRepVM
{
    public class BaseViewModel: INotifyPropertyChanged
    {
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
    public class SBRepModel : BaseViewModel
    {
        private string bufer_PATH = Path.Combine(Directory.GetCurrentDirectory(), @"bufer.obj");
        private string bufer_group_PATH = Path.Combine(Directory.GetCurrentDirectory(), @"buferGrTMP.obj");
        private DMesh3 _sourceModel;
        private bool _showSourceObject;
        private bool _showBoundaryEdges;
        private bool _showTriPlanarGroup;
        private bool _showLoopEdges;
        private Model3D convertedSourceModel;
        private Model3D boundaryEdgesModel;
        private Model3D triPlanarGroupModel;
        private Model3D loopEdgeModel;

        public DMesh3 SourceModel => _sourceModel;

        public Model3D MainMesh
        {
            get => GetOutputViewModel();
        }

        public bool ShowSourceObject
        {
            get => _showSourceObject;
            set
            {
                if(value == ShowSourceObject) return;
                _showSourceObject = value;
                OnPropertyChanged(nameof(MainMesh));
            }
        }
        public bool ShowBoundaryEdges
        {
            get => _showBoundaryEdges;
            set
            {
                if (value == _showBoundaryEdges) return;
                _showBoundaryEdges = value;
                OnPropertyChanged(nameof(MainMesh));
            }
        }
        public bool ShowTriPlanarGroup
        {
            get => _showTriPlanarGroup;
            set
            {
                if (value == _showTriPlanarGroup) return;
                _showTriPlanarGroup = value;
                OnPropertyChanged(nameof(MainMesh));
            }
        }
        public bool ShowLoopEdges
        {
            get => _showLoopEdges;
            set
            {
                if (value == _showLoopEdges) return;
                _showLoopEdges = value;
                OnPropertyChanged(nameof(MainMesh));
            }
        }

        public void SetModel(DMesh3 model)
        {
            _sourceModel = model;
            convertedSourceModel = ConvertToModel3D(SourceModel);

            var triPlanarGroup = SBRepBuilder.BuildPlanarGroups(model);
            boundaryEdgesModel = GenerateBoundaryEdgesFromEdgeIds(model, triPlanarGroup);
            triPlanarGroupModel = GenerateModelFromTriPlanarGroup(triPlanarGroup);
            loopEdgeModel = GenerateModelFromLoopEdge(
                model,
                SBRepBuilder.BuildLooppart(
                    model,
                    triPlanarGroup));
            OnPropertyChanged(nameof(MainMesh));
        }

        public void LoadModel(string path)
        {
            SetModel(StandardMeshReader.ReadMesh(path));
        }

        public Model3D GetOutputViewModel()
        {
            if(SourceModel == null) return null;
            var resultmodels = new Model3DGroup();
            if(ShowSourceObject && convertedSourceModel != null)
                resultmodels.Children.Add(convertedSourceModel);
            if(ShowBoundaryEdges && boundaryEdgesModel != null)
                resultmodels.Children.Add(boundaryEdgesModel);
            if (ShowTriPlanarGroup && triPlanarGroupModel != null)
                resultmodels.Children.Add(triPlanarGroupModel);
            if (ShowLoopEdges && triPlanarGroupModel != null)
                resultmodels.Children.Add(loopEdgeModel);

            return resultmodels;
        }


        #region ConvertDMeshToModel3D
        private Model3D ConvertToModel3D(DMesh3 model)
        {
            if(model == null) return null;
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
                var triangleMesh = MeshExtensions.ToWireframe(mesh, 0.005);
                DiffuseMaterial wireframe_material =
                    new DiffuseMaterial(Brushes.Black);
                var wireframe = new GeometryModel3D()
                {
                    Geometry = triangleMesh,
                    Material = new DiffuseMaterial(Brushes.Black)
                };
                var mat = new MaterialGroup();
                mat.Children.Add(new DiffuseMaterial(Brushes.Aqua));
                mat.Children.Add(new SpecularMaterial(Brushes.Blue, 80));
                var resultMesh = new GeometryModel3D()
                {
                    Geometry = mesh,
                    Material = new DiffuseMaterial(new SolidColorBrush(
                        GetColor(idGroup)))
                };
                resultmodels.Children.Add(resultMesh);
                resultmodels.Children.Add(wireframe);
            }
            return resultmodels;
        }

        private static Dictionary<int, Color> groupColors = new Dictionary<int, Color>();
        private Random rnd = new Random();
        private Color GetColor(int id)
        {
            if (groupColors.ContainsKey(id)) return groupColors[id];
            groupColors.Add(id, GetRandomColor());
            return groupColors[id];
        }
        private Color GetRandomColor(int id = -1)
        {
            return Color.FromRgb(
                            (byte)rnd.Next(256),
                            (byte)rnd.Next(256),
                            (byte)rnd.Next(256));
        }
        #endregion

        #region TriPlanarGroup Edges
        public Model3D GenerateBoundaryEdgesFromEdgeIds(DMesh3 mesh, IEnumerable<TriPlanarGroup> triPlanarGroups)
        {
            var resultmodels = new Model3DGroup();
            foreach (var triPlanar in triPlanarGroups)
            {
                resultmodels.Children.Add(
                    GenerateModel3DFromEdges(mesh, triPlanar));
            }

            return resultmodels;
        }
        public Model3D GenerateModel3DFromEdges(DMesh3 mesh, TriPlanarGroup triPlanar)
        {
            List<int> edgesIDs = triPlanar.GetBoundaryEdges();
            var valid = triPlanar.IsValid();
            var color = valid ? Colors.Blue : Colors.Red;
            return ModelFromEdge(mesh, edgesIDs, color, (valid ? 1 : 2));
        }

        public Model3D GenerateModelFromLoop(DMesh3 mesh, EdgeLoop loop)
        {
            return ModelFromEdge(mesh, loop.Edges, Colors.Green, 1);
        }

        public Model3D GenerateModelFromTriPlanarGroup(IEnumerable<TriPlanarGroup> planes)
        {
            var resultmodels = new Model3DGroup();
            foreach (var plans in planes)
                foreach (var loop in plans.GetLoops())
                {
                    resultmodels.Children.Add(GenerateModelFromLoop(SourceModel, loop));
                }
            return resultmodels;
        }
        #endregion

        public Model3D GenerateModelFromLoopEdge(DMesh3 mesh, IEnumerable<LoopEdge> edges)
        {
            var resultmodels = new Model3DGroup();
            foreach (var edge in edges)
            {
                resultmodels.Children.Add(
                    ModelFromEdge(
                        mesh,
                        edge.edgeIDs,
                        GetRandomColor(edge.ID), 
                        3));
            }
            return resultmodels;
        }

        #region VisualBuilder
        public Model3D ModelFromEdge(DMesh3 mesh, 
            IEnumerable<int> edgesIDs,
            Color color, double diameterScale = 1)
        {
            var theta = 4; 
            var rad = diameterScale * 1.3 * 0.01;
            var phi = 4;
            var builder = new MeshBuilder(true, true);
            var points = new List<Point3D>();
            var edges = new List<int>();
            foreach (var eid in edgesIDs)
            {
                var idx = mesh.GetEdgeV(eid);
                var a = mesh.GetVertex(idx.a);
                var b = mesh.GetVertex(idx.b);
                var ad = new Point3D(a.x, a.y, a.z);
                var bd = new Point3D(b.x, b.y, b.z);
                if (!points.Contains(ad))
                {
                    points.Add(ad);
                }
                if (!points.Contains(bd))
                {
                    points.Add(bd);
                }
                edges.Add(points.FindIndex(x => x.Equals(ad)));
                edges.Add(points.FindIndex(x => x.Equals(bd)));
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
        #endregion
    }

    public class SBRepViewModel : BaseViewModel
    {
        private SBRepModel data;

        public SBRepModel Data => data;

        protected Lazy<DelegateCommand> _loadModelCommand;
        public ICommand LoadModelCommand => _loadModelCommand.Value;

        public SBRepViewModel()
        {
            _loadModelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(LoadModel));
            data = new SBRepModel();
        }
        private void LoadModel(object obj)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "3d Objects|*.obj;*.stl";
            if (ofd.ShowDialog() != true)
                return;
            // получаем выбранный файл
            string filename = ofd.FileName;
            data.LoadModel(filename);
        }
    }
}
