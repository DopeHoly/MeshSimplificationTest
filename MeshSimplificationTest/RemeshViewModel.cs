using g3;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MeshSimplificationTest
{
    public class RemeshViewModel : INotifyPropertyChanged
    {
        //Path to the model file
        //private const string MODEL_PATH = @"C:\GitProjects\Frost3\samples\stl\dragon.stl";
        //private const string MODEL_PATH = @"C:\Users\menin\Documents\cilinder.obj";
        //private const string MODEL_PATH = @"C:\Users\User\Downloads\145U_14Part_withoutRound_withoutMerge_withoutCorrect.stl";
        private string MODEL_PATH = Path.Combine(Directory.GetCurrentDirectory(),"cube.stl");
        private string bufer_PATH = Path.Combine(Directory.GetCurrentDirectory(), @"bufer.obj");
        private string bufer_group_PATH = Path.Combine(Directory.GetCurrentDirectory(), @"buferGrTMP.obj");
        private Random rnd = new Random();
        private DMesh3 baseModel { get; set; }
        private DMesh3 renderModel { get; set; }
        private DMesh3 prevModel { get; set; }
        public RemeshTool RemeshTool { get; set; }

        private int _triangleFullCount;
        public int TriangleFullCount
        {
            get => _triangleFullCount;
            set
            {
                _triangleFullCount = value;
                OnPropertyChanged();
            }
        }
        public int GroupCount { get; set; }

        private int _triangleCurrentCount;
        public int TriangleCurrentCount
        {
            get => _triangleCurrentCount;
            set
            {
                _triangleCurrentCount = value;
                OnPropertyChanged();
            }
        }

        public bool MeshValid
        {
            get
            {
                return renderModel?.CheckValidity(eFailMode: FailMode.ReturnOnly) ?? false;
            }
        }
        public double CalcEdgeLength
        {
            get
            {
                return GeometryUtils.GetTargetEdgeLength(renderModel); ;
            }
        }



        protected Lazy<DelegateCommand> _applyCommand;
        public ICommand ApplyCommand => _applyCommand.Value; 

        protected Lazy<DelegateCommand> _cancelCommand;
        public ICommand CancelCommand => _cancelCommand.Value; 
        
        protected Lazy<DelegateCommand> _returnToBaseModelCommand;
        public ICommand ReturnToBaseModelCommand => _returnToBaseModelCommand.Value;
        
        protected Lazy<DelegateCommand> _fixNormalsCommand;
        public ICommand FixNormalsCommand => _fixNormalsCommand.Value;

        protected Lazy<DelegateCommand> _loadModelCommand;
        public ICommand LoadModelCommand => _loadModelCommand.Value;

        protected Lazy<DelegateCommand> _saveModelCommand;
        public ICommand SaveModelCommand => _saveModelCommand.Value;

        protected Lazy<DelegateCommand> _removeZeroTriangle;
        public ICommand RemoveZeroTriangle => _removeZeroTriangle.Value;

        protected Lazy<DelegateCommand> _refreshViewCommand;
        public ICommand RefreshViewCommand => _refreshViewCommand.Value;

        protected Lazy<DelegateCommand> _setPrevModelCommand;
        public ICommand SetPrevModelCommand => _setPrevModelCommand.Value;

        private Model3D _mainMesh;
        public Model3D MainMesh
        {
            get => _mainMesh;
            set
            {
                _mainMesh = value;
                TriangleFullCount = baseModel.TriangleCount;
                TriangleCurrentCount = renderModel.TriangleCount;
                GroupCount = FaceGroupUtil.FindTriangleSetsByGroup(renderModel).Length;         
                OnPropertyChanged(nameof(GroupCount));
                OnPropertyChanged(nameof(MeshValid));
                OnPropertyChanged(nameof(CalcEdgeLength));
                ResetPB();
                OnPropertyChanged();
            }
        }

        private Model3D _debugLayer;
        public Model3D DebugLayer
        {
            get => _debugLayer;
            set
            {
                _debugLayer = value;
                OnPropertyChanged();
            }
        }



        private double progressProcent;
        public double ProgressProcent
        {
            get => progressProcent;
            set
            {
                progressProcent = value * 100;
                OnPropertyChanged();
            }
        }
        private bool isIndeterminate;
        public bool IsIndeterminate
        {
            get => isIndeterminate;
            set
            {
                isIndeterminate = value;
                OnPropertyChanged();
            }
        }
        private CancellationTokenSource token;
        public CancellationTokenSource Token
        {
            get => token;
            set
            {
                token = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CancelCommand));
            }
        }

        private void ResetPB()
        {
            ProgressProcent = 0;
            IsIndeterminate = false;
        }


        public RemeshViewModel()
        {
            _applyCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand((o)=>ApplyCommandExecute()));

            _returnToBaseModelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(ResetModel));

            _cancelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(CancelCommandEx, CancelCommandCanEx));

            _loadModelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(LoadModel));

            _saveModelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(SaveModel));

            _fixNormalsCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(FixNormalsCommandExecute));

            _removeZeroTriangle = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(RemoveZeroTriangleCommandExecute));

            _refreshViewCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand((o) => Render()));

            _setPrevModelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(SetPrevModelExecute));

            RemeshTool = new RemeshTool();

            LoadModel(MODEL_PATH);            
            Render();
        }

        private bool CancelCommandCanEx(object arg)
        {
            return Token != null && Token.Token.CanBeCanceled;
        }

        private void CancelCommandEx(object obj)
        {
            if (Token.Token.CanBeCanceled) Token.Cancel();
        }

        private void LoadModel(string path)
        {
            baseModel = StandardMeshReader.ReadMesh(path);
            TriangleFullCount = baseModel?.TriangleCount ?? 0;
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
                var triangleMesh = MeshExtensions.ToWireframe(mesh, 0.0005);
                DiffuseMaterial wireframe_material =
                    new DiffuseMaterial(Brushes.Black);
                var wireframe = new GeometryModel3D()
                {
                    Geometry = triangleMesh,
                    Material = new DiffuseMaterial(Brushes.Black)
                };
                //var mat = new MaterialGroup();
                //mat.Children.Add(new DiffuseMaterial(Brushes.Aqua));
                //mat.Children.Add(new SpecularMaterial(Brushes.Blue, 80));
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

        private Model3D ConvertToModel3D(DMesh3 model)
        {
            Model3DGroup resultmodels = null;
            try
            {
                WriteOptions writeOptions = new WriteOptions();
                writeOptions.bPerVertexColors = true;

                int[][] group_tri_sets = FaceGroupUtil.FindTriangleSetsByGroup(model);
                if(group_tri_sets.Length > 0)
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

        private async void ApplyCommandExecute()
        {
            IProgress<double> progress = new Progress<double>((p) => ProgressProcent = p);
            Token = new CancellationTokenSource();
            try
            {
                renderModel = await RemeshTool.RemeshAsync(baseModel, Token.Token, progress);
            }
            catch {}
            finally
            {
                Token = null;
                Render();
            }
        }
        private void FixNormalsCommandExecute(object o)
        {
            RemeshTool.FixAndRepairMesh(baseModel);
            RemeshTool.FixAndRepairMesh(renderModel);
            Render();
        }
        private void RemoveZeroTriangleCommandExecute(object o)
        {
            //RemeshTool.DeleteDegenerateTriangle(baseModel, RemeshTool.Angle);
            prevModel = new DMesh3(renderModel);
            GeometryUtils.DeleteDegenerateTriangle(renderModel, RemeshTool.Angle);
            Render();
        }

        private void SetPrevModelExecute(object o)
        {
            renderModel = new DMesh3(prevModel);
            Render();
        }

        private void ResetModel(object param)
        {
            IsIndeterminate = true;
            renderModel = new DMesh3(baseModel);
            GenerateDebugLayer();
            MainMesh = ConvertToModel3D(renderModel);
        }

        private void LoadModel(object obj)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "3d Objects|*.obj;*.stl";
            if (ofd.ShowDialog() != true)
                return;
            // получаем выбранный файл
            string filename = ofd.FileName;
            LoadModel(filename);
            ResetModel(null);
        }
        private void SaveModel(object obj)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "3d Objects|*.obj;";
            if (sfd.ShowDialog() != true)
                return;
            // получаем выбранный файл
            string filename = sfd.FileName;
            WriteOptions writeOptions = new WriteOptions();
            writeOptions.bPerVertexColors = true;
            writeOptions.bWriteGroups = true;

            StandardMeshWriter.WriteFile(
                filename,
                new List<WriteMesh>() { new WriteMesh(renderModel) },
                writeOptions);
        }

        private void Render()
        {
            if (baseModel == null) return;
            renderModel = renderModel == null ? new DMesh3(baseModel) : renderModel;
            GenerateDebugLayer();
            OnPropertyChanged(nameof(MeshValid));
            OnPropertyChanged(nameof(CalcEdgeLength));
            MainMesh = ConvertToModel3D(renderModel);
        }

        private void GenerateDebugLayer()
        {
            var cons = RemeshTool.GetMeshConstraints(renderModel);
            if (cons == null)
            {
                DebugLayer = null;
                return;
            }
            var builder = new MeshBuilder(true, true);
            var builderDict = new Dictionary<int, MeshBuilder>();
            var builderVtxIdDict = new Dictionary<int, List<int>>();
            var rad = 0.01;
            var theta = 4;
            var phi = 4;
            var contsFlag = 99900;
            var points = new List<Point3D>();
            var edges = new List<int>();
            var cnt = 0;
            foreach (var eid in renderModel.EdgeIndices())
            {
                if (cons.GetEdgeConstraint(eid).Equals(EdgeConstraint.Unconstrained)) continue;
                var idx = renderModel.GetEdgeV(eid);
                var a = renderModel.GetVertex(idx.a);
                var b = renderModel.GetVertex(idx.b);
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
                ++cnt;
            }
            builder.AddEdges(points, edges, 0.001, theta);
            foreach (KeyValuePair<int, VertexConstraint> isd in cons.VertexConstraintsItr())
            {
                var id = isd.Key;
                //if (id == 2404) continue;
                var constrain = cons.GetVertexConstraint(id);
                var builderID = constrain.FixedSetID;
                if(builderID >= 100000 && builderID < 200000)
                {
                    builderID = 100000;
                }
                if (builderID >= 200000)
                {
                    builderID = 200000;
                }
                var vtx = renderModel.GetVertex(id);
                if (!builderDict.ContainsKey(builderID))
                {
                    builderDict.Add(builderID, new MeshBuilder(true, true));
                    builderVtxIdDict.Add(builderID, new List<int>());
                }
                builderVtxIdDict[builderID].Add(id);
                builderDict[builderID].AddEllipsoid(new Point3D(vtx.x, vtx.y, vtx.z), rad, rad, rad, theta, phi);
            }
            var debugLayer = new Model3DGroup();
            var problemId = new List<int>();
            foreach (var item in builderVtxIdDict.Values)
            {
                if(item.Count == 1)
                {
                    problemId.Add(item[0]);
                }
            }

            var modelWarframe = new GeometryModel3D()
            {
                Geometry = builder.ToMesh(),
                Material = new DiffuseMaterial(new SolidColorBrush(Colors.Blue))
            };
            debugLayer.Children.Add(modelWarframe);
            foreach (var item in builderDict)
            {
                var color = GetColor(item.Key);
                if(builderVtxIdDict[item.Key].Count == 1)
                {
                    color = Colors.White;
                }
                if (item.Key == 100000)
                {
                    color = Colors.Red;
                }
                if (item.Key == 200000)
                {
                    color = Colors.Purple;
                }
                if (item.Key == contsFlag)
                {
                    color = Colors.Lime;
                }
                if (item.Key == 99901)//0
                {
                    color = Colors.Yellow;
                }
                if (item.Key == 99902)//1
                {
                    color = Colors.Lime;
                }
                if (item.Key == 99903)//2
                {
                    color = Colors.LimeGreen;
                }
                if (item.Key == 99904)//3
                {
                    color = Colors.OrangeRed;
                }
                if (item.Key == 99905)//4
                {
                    color = Colors.DarkOrange;
                }
                if (item.Key == 99906)//5
                {
                    color = Colors.Orchid;
                }
                var model = new GeometryModel3D()
                {
                    Geometry = item.Value.ToMesh(),
                    Material = new DiffuseMaterial(new SolidColorBrush(color))
                };
                debugLayer.Children.Add(model);
            }
            DebugLayer = debugLayer;
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

    public interface IMeshHost
    {
        void SetModel(DMesh3 render);
        void Render(DMesh3 model);
        void ResetView();
    }

    public interface IMeshWidget
    {
        bool Visible { get; set; }
        void SetModel(DMesh3 model);
        DMesh3 GetModel();
        void SetParent(IMeshHost host);
        void Close();
    }

    public class RemeshWidget : INotifyPropertyChanged, IMeshWidget
    {
        public bool _visible;
        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                OnPropertyChanged();
            }
        }
        private DMesh3 baseModel { get; set; }
        private DMesh3 renderModel { get; set; }
        public RemeshTool RemeshTool { get; set; }

        public IMeshHost host;


        protected Lazy<DelegateCommand> _applyCommand;
        public ICommand ApplyCommand => _applyCommand.Value;

        protected Lazy<DelegateCommand> _cancelCommand;
        public ICommand CancelCommand => _cancelCommand.Value;

        protected Lazy<DelegateCommand> _fixNormalsCommand;
        public ICommand FixNormalsCommand => _fixNormalsCommand.Value;

        protected Lazy<DelegateCommand> _okCommand;
        public ICommand OkCommand => _okCommand.Value;

        private int _triangleFullCount;
        public int TriangleFullCount
        {
            get => _triangleFullCount;
            set
            {
                _triangleFullCount = value;
                OnPropertyChanged();
            }
        }
        public int GroupCount { get; set; }

        private int _triangleCurrentCount;
        public int TriangleCurrentCount
        {
            get => _triangleCurrentCount;
            set
            {
                _triangleCurrentCount = value;
                OnPropertyChanged();
            }
        }

        private double progressProcent;
        public double ProgressProcent
        {
            get => progressProcent;
            set
            {
                progressProcent = value;
                OnPropertyChanged();
            }
        }
        private bool isIndeterminate;
        public bool IsIndeterminate
        {
            get => isIndeterminate;
            set
            {
                isIndeterminate = value;
                OnPropertyChanged();
            }
        }
        private CancellationTokenSource token;
        public CancellationTokenSource Token
        {
            get => token;
            set
            {
                token = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CancelCommand));
            }
        }

        public bool MeshValid
        {
            get
            {
                return renderModel?.CheckValidity(eFailMode: FailMode.ReturnOnly) ?? false;
            }
        }

        public RemeshWidget()
        {
            Visible = false;

            _applyCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand((o) => ApplyCommandExecute()));

            _cancelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(CancelCommandEx, CancelCommandCanEx));

            _fixNormalsCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(FixNormalsCommandExecute));

            _okCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(OkCommandEx));

            RemeshTool = new RemeshTool();
        }
        private void OkCommandEx(object obj)
        {
            host.SetModel(renderModel ?? baseModel);
            Close();
        }


        private bool CancelCommandCanEx(object arg)
        {
            return Token != null && Token.Token.CanBeCanceled;
        }

        private void CancelCommandEx(object obj)
        {
            if (Token.Token.CanBeCanceled) Token.Cancel();
        }

        private async void ApplyCommandExecute()
        {
            IProgress<double> progress = new Progress<double>((p) => ProgressProcent = p);
            Token = new CancellationTokenSource();
            try
            {
                renderModel = await RemeshTool.RemeshAsync(baseModel, Token.Token, progress);
            }
            catch { }
            finally
            {
                Token = null;
                Render();
            }
        }
        private void FixNormalsCommandExecute(object o)
        {
            RemeshTool.FixNormals(baseModel);
            RemeshTool.FixNormals(renderModel);
            Render();
        }
        private void Render()
        {
            host.Render(renderModel ?? baseModel);
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

        public void SetModel(DMesh3 model)
        {
            baseModel = model;
            renderModel = baseModel;
            Render();
        }

        public DMesh3 GetModel()
        {
            return renderModel;
        }

        public void SetParent(IMeshHost host)
        {
            this.host = host;
        }

        public void Close()
        {
            host.ResetView();
            Visible = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
