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
using SBRep;
using static SBRep.SBRepBuilder;
using System.Collections.ObjectModel;
using System.Xml.Serialization;
using SBRep.Utils;
using System.Security.Cryptography;
using GeometryLib;
using PlyReader = GeometryLib.PlyReader;
using gs;

namespace MeshSimplificationTest.SBRepVM
{
    public class Model3DLayerVM : BaseViewModel
    {
        private SBRepModel Owner;
        private bool _visibility;
        private Model3D _model;
        private string _name;

        public string Name
        {
            get => _name;
            set => _name = value;
        }
        public bool Visibility
        {
            get => _visibility;
            set
            {
                if (value == _visibility) return;
                _visibility = value;
                OnPropertyChanged();
                Owner.LayerVisibilityChanged();
            }
        }
        public Model3D Model
        {
            get => _model;
            set => _model = value;
        }

        public Model3DLayerVM(SBRepModel owner)
        {
            this.Owner = owner;
        }
    }

    public class ContourVM
    {
        public string Name { get; set; }
        public List<Vector2d> Value { get; set; }

        public static ContourVM GetFromText(string path)
        {
            if (!File.Exists(path)) return null;
            var fi = new FileInfo(path);
            if (fi.Extension != ".cnt")
                return null;

            return new ContourVM()
            {
                Name = fi.Name,
                Value = LoadContour(path)
            };
        }
        private static void SaveContour(string path, List<Vector2d> contour)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Vector2d>));
            var dict = new FileInfo(path);
            if (!dict.Directory.Exists) dict.Directory.Create();
            using (StreamWriter writer = new StreamWriter(path, false))
            {
                xmlSerializer.Serialize(writer, contour);
            }

        }
        private static List<Vector2d> LoadContour(string path)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Vector2d>));
            List<Vector2d> result = null;
            using (var reader = new StreamReader(path))
            {
                result = xmlSerializer.Deserialize(reader) as List<Vector2d>;
            }
            return result;
        }
    }

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
        public ObservableCollection<Model3DLayerVM> ModelsVM { get; set; }

        public double XOffset { get; set; }
        public double YOffset { get; set; }

        public List<Vector2d> Contour;
        public ObservableCollection<ContourVM> Contours { get; set; }

        public SBRepModel()
        {
            ModelsVM = new ObservableCollection<Model3DLayerVM>();
            Contours = new ObservableCollection<ContourVM>();
            //var contour = LoadContour("D:\\ContourProjection\\contour + 5d7fb584-f43b-4ece-b258-c0cb252f01e9.cnt");
            var contour = new List<Vector2d>();
            Contour = contour;

            //var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\testTop.ply";
            //var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\testBot.ply";

            //var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_top.ply";
            //var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_bot.ply";

            var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\220010_top.ply";
            var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\220010_bot.ply";

            //var vertices = new List<g3.Vector2d>();
            //var edges = new List<g3.Index2i>();

            //vertices.Add(new g3.Vector2d(1, 2));
            //vertices.Add(new g3.Vector2d(1, 3));
            //vertices.Add(new g3.Vector2d(2, 3));
            //vertices.Add(new g3.Vector2d(2, 2));

            //vertices.Add(new g3.Vector2d(3, 1));
            //vertices.Add(new g3.Vector2d(3, 4));
            //vertices.Add(new g3.Vector2d(0, 0));
            //vertices.Add(new g3.Vector2d(0, 0));

            //edges.Add(new g3.Index2i(0, 1));
            //edges.Add(new g3.Index2i(1, 2));
            //edges.Add(new g3.Index2i(2, 3));
            //edges.Add(new g3.Index2i(3, 0));

            //edges.Add(new g3.Index2i(0, 4));
            //edges.Add(new g3.Index2i(4, 5));
            //edges.Add(new g3.Index2i(5, 1));

            //SbrepVizualizer.ShowEdges(vertices, edges);

            //IEnumerable<g3.Vector2d> triVertices = null;
            //IEnumerable<g3.Index3i> triangulationTri = null;


            //var noError = TriangulationCaller.CDT(
            //    vertices, edges,
            //    out triVertices, out triangulationTri);

            //SbrepVizualizer.ShowTriangles(vertices, triangulationTri);
            var path = @"D:\Задачи\Газпром грунты\формат ply+свойства\";

            var dir = new DirectoryInfo(path);
            if (!dir.Exists)
                return;

            var plyReader = new PlyReader();
            var files = dir.GetFiles("*.ply");
            var offsetX = 526701.42214999557;
            var offsetY = 1623689.0564871004;
            foreach ( var file in files)
            {
                var mesh = plyReader.Read(file.FullName);
                ModelsVM.Add(new Model3DLayerVM(this)
                {
                    Name = file.Name,
                    Visibility = true,
                    Model = ConvertToModel3D(CompaqMesh(mesh, 0.02, 0.02, 0.02, -offsetX, -offsetY)),
                });
            }
            //var plyReader = new PlyReader();
            //var meshA = plyReader.Read(pathA);
            //var meshB = plyReader.Read(pathB);
            ////SetModel(PlanesStapler.Stapler(meshA, meshB,
            ////    visualizer: (x, y) => SbrepVizualizer.ShowEdges(x, y),
            ////    triVisualizer: (x, y) => SbrepVizualizer.ShowTriangles(x, y)));
            //SetModel(PlanesStapler.TestStapler(meshA, meshA));
            //SetSBRepModel(PlanesStapler.SbrepTestStapler(meshA, meshA));
        }

        private DMesh3 CompaqMesh(DMesh3 mesh, double xScale, double yScale, double zScale, double offsetX = 0, double offsetY= 0)
        {
            var vertices = mesh.VertexIndices()
                .Select(x => mesh.GetVertex(x))
                .Select(x => new Vector3d((x.x + offsetX) * xScale, (x.y + YOffset) * yScale, x.z * zScale))
                .ToList();
            var triangles = mesh.TriangleIndices().Select(x => 
            {
                Index3i tri = mesh.GetTriangle(x);
                if (mesh.GetTriNormal(x).z < 0)
                    tri = new Index3i(tri.a, tri.c, tri.b);
                return tri;
            }).ToList();
            var faces = mesh.TriangleIndices().Select(x => mesh.GetTriangleGroup(x)).ToList();

            var result = DMesh3Builder.Build<Vector3d, Index3i, Vector3f>(
                vertices,
                triangles//,
                //TriGroups: faces
                );

            //var meshRepairOrientation = new MeshRepairOrientation(result);
            //meshRepairOrientation.OrientComponents();
            //meshRepairOrientation.SolveGlobalOrientation();
            return result;
        }

        public DMesh3 SourceModel => _sourceModel;

        public Model3D MainMesh
        {
            get => GetOutputViewModel();
        }

        private DMesh3 projectionObjectMesh;
        public bool MeshValid
        {
            get
            {
                return projectionObjectMesh?.CheckValidity(eFailMode: FailMode.ReturnOnly) ?? false;
            }
        }


        public void SetModel(DMesh3 model)
        {
            _sourceModel = model;


            ModelsVM.Add(new Model3DLayerVM(this)
            {
                Name = "Исходный объект",
                Model = ConvertToModel3D(SourceModel),
            });

            //var contour = Contour;

            var contour1 = new List<Vector2d>();
            contour1.Add(new Vector2d(-5, -5));
            contour1.Add(new Vector2d(-5, 100));
            contour1.Add(new Vector2d(100, -5));
            //Contours.Add(new ContourVM()
            //{
            //    Name = "TestContour",
            //    Value = contour1,
            //});

            //var contour2 = new List<Vector2d>();
            //contour2.Add(new Vector2d(3, 1));
            //contour2.Add(new Vector2d(3, 3));
            //contour2.Add(new Vector2d(1, 2));

            var triPlanarGroup = SBRepBuilder.BuildPlanarGroups(model);
            SBRepObject projectionObject = null;
            try
            {
                var sbrep = SBRepBuilder.Convert(model);
                projectionObject = new SBRepObject(sbrep);
                ModelsVM.Add(new Model3DLayerVM(this)
                {
                    Name = "Триангулированный объект",
                    Model = ConvertToModel3D(SBRepToMeshBuilder.ConvertParallel(sbrep)),
                });
            }
            catch (Exception ex)
            {
                ;
            }
            foreach (var contour in Contours)
            {
                //if (contour == Contours.Last()) continue;
                try
                {
                    projectionObject = projectionObject?.ContourProjection(contour.Value, true);
                    projectionObjectMesh = SBRepToMeshBuilderV2.ConvertParallel(projectionObject);
                    ModelsVM.Add(new Model3DLayerVM(this)
                    {
                        Name = "Триангулированный объект c проекцией " + contour.Name,
                        Model = ConvertToModel3D(projectionObjectMesh),
                    });
                    //ModelsVM.Add(new Model3DLayerVM(this)
                    //{
                    //    Name = "Грани проекции",
                    //    Model = GenerateModelFormSBRepObjectEdges(projectionObject)
                    //});
                }
                catch (OutOfMemoryException ex)
                //catch (Exception ex)
                {
                    ;
                }
            }
            //SBRepIO.Write(projectionObject, @"D:\Проекция контура\test2.sbrep");
            //projectionObject = projectionObject.ContourProjection(contour2, true);

            //var boundaryEdgesModel = GenerateBoundaryEdgesFromEdgeIds(model, triPlanarGroup);
            //ModelsVM.Add(new Model3DLayerVM(this)
            //{
            //    Name = "Грани объекта",
            //    Model = boundaryEdgesModel,
            //});

            //var triPlanarGroupModel = GenerateModelFromTriPlanarGroup(triPlanarGroup);
            //ModelsVM.Add(new Model3DLayerVM(this)
            //{
            //    Name = "Петли",
            //    Model = triPlanarGroupModel,
            //});

            //var loopEdgeModel = GenerateModelFromLoopEdge(
            //    model,
            //    SBRepBuilder.BuildVerges(
            //        model,
            //        triPlanarGroup));
            //ModelsVM.Add(new Model3DLayerVM(this)
            //{
            //    Name = "Грани петель",
            //    Model = loopEdgeModel,
            //});

            //var sbrep_loops = GenerateModelFromObjectLoop(sbrep);
            //ModelsVM.Add(new Model3DLayerVM(this)
            //{
            //    Name = "Петли из sbrep",
            //    Model = sbrep_loops,
            //});
            //ModelsVM.Add(new Model3DLayerVM(this)
            //{
            //    Name = "Триангулированный объект",
            //    Model = ConvertToModel3D(SBRepToMeshBuilder.ConvertParallel(sbrep)),
            //});
            //ModelsVM.Add(new Model3DLayerVM(this)
            //{
            //    Name = "Точки проекции",
            //    Model = GenerateModelFromModelsVerices(projectionObject)
            //});
            //ModelsVM.Add(new Model3DLayerVM(this)
            //{
            //    Name = "Грани проекции",
            //    Model = GenerateModelFormSBRepObjectEdges(projectionObject)
            //});
            foreach (var contour in Contours)
            {

                ModelsVM.Add(new Model3DLayerVM(this)
                {
                    Name = "Контур проекции: " + contour.Name,
                    Model = GenerateModelFrom2dContour(contour.Value, Colors.Green, diameterScale: 4)
                });
            }
            
            OnPropertyChanged(nameof(MainMesh)); 
            OnPropertyChanged(nameof(ModelsVM));
            OnPropertyChanged(nameof(MeshValid));
        }

        public void LoadModel(string path)
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) return;
            var parentDir = fi.Directory;
            var contoursFiles = parentDir.GetFiles("*.cnt");

            Contours.Clear();
            foreach (var file in contoursFiles)
            {
                var contour = ContourVM.GetFromText(file.FullName);
                if (contour == null) continue;
                Contours.Add(contour);
            }


            ModelsVM.Clear();
            SetModel(StandardMeshReader.ReadMesh(path));
        }


        public void SetSBRepModel(SBRepObject model)
        {
            var mesh = SBRepToMeshBuilderV2.ConvertParallel(model);
            ModelsVM.Add(new Model3DLayerVM(this)
            {
                Name = "Триангулированный объект",                
                Visibility = true,
                Model = ConvertToModel3D(mesh),
            });
            ModelsVM.Add(new Model3DLayerVM(this)
            {
                Name = "Грани проекции",
                Visibility = true,
                Model = GenerateModelFormSBRepObjectEdges(model)
            });
            OnPropertyChanged(nameof(MainMesh));
            OnPropertyChanged(nameof(ModelsVM));
            OnPropertyChanged(nameof(MeshValid));
        }

        public void LoadSBRepModel(string path)
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) return;

            ModelsVM.Clear();
            SetSBRepModel(model: SBRepIO.Read(path));
        }

        public Model3D GetOutputViewModel()
        {
            //if(SourceModel == null) return null;
            var resultmodels = new Model3DGroup();

            foreach (var model in ModelsVM)
            {
                if(model.Visibility)
                    resultmodels.Children.Add(model.Model);
            }

            return resultmodels;
        }

        public void ApplyOffset()
        {
            var offsetPoint = new Vector2d(XOffset, YOffset);   
            Contour = Contour.Select(point => point + offsetPoint).ToList();
            SetModel(SourceModel);
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
                        resultmodels.Children.Add(DMesh3ColorTriangleWithoutNeighbor(model, Colors.Yellow));
                        resultmodels.Children.Add(DMesh3ColorTriangleZeroNormal(model, Colors.GreenYellow));
                        ++cnt;
                    }
                }
                else
                {
                    StandardMeshWriter.WriteFile(
                        bufer_PATH,
                        new List<WriteMesh>() { new WriteMesh(model) },
                        writeOptions);
                    resultmodels = LoadTriangleMesh(bufer_PATH, rnd.Next(1000));
                    resultmodels.Children.Add(DMesh3ColorTriangleWithoutNeighbor(model, Colors.CadetBlue));
                    resultmodels.Children.Add(DMesh3ColorTriangleZeroNormal(model, Colors.GreenYellow));
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

        public Model3D GenerateModelFromLoop(DMesh3 mesh, IEnumerable<int> loop)
        {
            return ModelFromEdge(mesh, loop, Colors.Green, 1);
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


        public Model3D DMesh3ColorTriangleWithoutNeighbor(DMesh3 mesh, Color color, double diameterScale = 1)
        {
            var theta = 4;
            var rad = diameterScale * 1.3 * 0.01;
            var phi = 4;
            var builder = new MeshBuilder(true, true);
            var points = new List<Point3D>();
            var edges = new List<int>();
            foreach (var eid in mesh.EdgeIndices())
            {
                if (eid == 27674)
                    ;
                var edgeTri = mesh.GetEdgeT(eid);

                if (edgeTri.a == -1 || edgeTri.b == -1)
                {
                    var edgePointIndeces = mesh.GetEdgeV(eid);
                    var a = mesh.GetVertex(edgePointIndeces.a);
                    var b = mesh.GetVertex(edgePointIndeces.b);
                    var pointA = new Point3D(a.x, a.y, a.z);
                    var pointB = new Point3D(b.x, b.y, b.z);
                    points.Add(pointA);
                    points.Add(pointB);
                }
            }
            //builder.AddEdges(points, edges, 0.01 * diameterScale, theta);
            foreach (var point in points)
                builder.AddEllipsoid(point, rad, rad, rad, theta, phi);
            return new GeometryModel3D()
            {
                Geometry = builder.ToMesh(),
                Material = new DiffuseMaterial(new SolidColorBrush(color))
            };
        }
        public Model3D DMesh3ColorTriangleZeroNormal(DMesh3 mesh, Color color, double diameterScale = 1)
        {
            var theta = 4;
            var rad = diameterScale * 1.3 * 0.01;
            var phi = 4;
            var builder = new MeshBuilder(true, true);
            var points = new List<Point3D>();
            var edges = new List<int>();
            foreach (var tid in mesh.TriangleIndices())
            {
                var normal = mesh.GetTriNormal(tid);

                if (SBRepBuilder.Vector3dEqual(normal, Vector3d.Zero))
                {
                    var a = Vector3d.Zero;
                    var b = Vector3d.Zero;
                    var c = Vector3d.Zero;
                    mesh.GetTriVertices(tid, ref a, ref b, ref c);

                    var pointA = new Point3D(a.x, a.y, a.z);
                    var pointB = new Point3D(b.x, b.y, b.z);
                    var pointC = new Point3D(c.x, c.y, c.z);
                    points.Add(pointA);
                    points.Add(pointB);
                    points.Add(pointC);
                }
            }
            //builder.AddEdges(points, edges, 0.01 * diameterScale, theta);
            foreach (var point in points)
                builder.AddEllipsoid(point, rad, rad, rad, theta, phi);
            return new GeometryModel3D()
            {
                Geometry = builder.ToMesh(),
                Material = new DiffuseMaterial(new SolidColorBrush(color))
            };
        }

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

        public Model3D GenerateModelFromObjectLoop(SBRepObject sbrep)
        {
            var resultmodels = new Model3DGroup();
            foreach (var loop in sbrep.Loops)
            {
                var lid = loop.ID;
                //if (lid != 2)
                //    continue;
                resultmodels.Children.Add(
                    ModelFromEdge(
                        sbrep,
                        sbrep.GetEdgesIdFromLoopId(lid),
                        GetColor(lid)));
            }
            return resultmodels;
        }

        public Model3D GenerateModelFromModelsVerices(SBRepObject sbrep)
        {
            return ModelFromVertices(sbrep, Colors.Red, 3);
        }

        public Model3D GenerateModelFormSBRepObjectEdges(SBRepObject sbrep)
        {
            var edgesIds = sbrep.Edges.GetIndexes();
            return ModelFromEdge(sbrep, edgesIds, Colors.Red, 0.1);
        }

        public Model3D GenerateModelFrom2dContour(IEnumerable<Vector2d> contour, Color color, double contsZ = 0, double diameterScale = 1)
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
            for(int i = 0; i < points.Count; ++i)
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

        #region VisualBuilder
        public Model3D ModelFromVertices(SBRepObject mesh,
            Color color, double diameterScale = 1)
        {
            var theta = 4;
            var rad = diameterScale * 1.3 * 0.01;
            var phi = 4;
            var builder = new MeshBuilder(true, true);
            var points = new List<Point3D>();
            var edges = new List<int>();
            foreach(var vtx in mesh.Vertices)
            {
                var a = vtx.Coordinate;
                var point = new Point3D(a.x, a.y, a.z);
                points.Add(point);
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


        public Model3D ModelFromEdge(SBRepObject mesh,
            IEnumerable<int> edgesIDs,
            Color color, double diameterScale = 1)
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
                var curentColor = Colors.Gray;
#if DEBUG
                curentColor = GetColor(mesh.Edges[eid].Parent);
                //curentColor = mesh.Edges[eid].Color;
#endif
                if (!edgesColorized.ContainsKey(curentColor))
                    edgesColorized.Add(curentColor, new List<int>());
                var idx = mesh.Edges[eid].Vertices;
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

        #endregion

        public void LayerVisibilityChanged()
        {
            OnPropertyChanged(nameof(MainMesh));
        }
    }

    public class SBRepViewModel : BaseViewModel
    {
        private SBRepModel data;

        public SBRepModel Data => data;

        protected Lazy<DelegateCommand> _loadModelCommand;
        protected Lazy<DelegateCommand> _loadSBRepModelCommand;
        protected Lazy<DelegateCommand> _ApplyOffsetCommand;
        public ICommand LoadModelCommand => _loadModelCommand.Value;
        public ICommand ApplyOffset => _ApplyOffsetCommand.Value;
        public ICommand LoadSBRepCommand => _loadSBRepModelCommand.Value;

        public SBRepViewModel()
        {
            _loadModelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(LoadModel));
            _loadSBRepModelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(LoadSBRepModel));
            _ApplyOffsetCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(x => data.ApplyOffset()));
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
        private void LoadSBRepModel(object obj)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "SBRep Objects|*.sbrep;";
            if (ofd.ShowDialog() != true)
                return;
            // получаем выбранный файл
            string filename = ofd.FileName;
            data.LoadSBRepModel(filename);
        }
    }
}
