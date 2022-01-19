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
    public class MasterViewModel : INotifyPropertyChanged
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


        
        protected Lazy<DelegateCommand> _applyCommand;
        public ICommand ApplyCommand => _applyCommand.Value; 

        protected Lazy<DelegateCommand> _cancelCommand;
        public ICommand CancelCommand => _cancelCommand.Value; 
        
        protected Lazy<DelegateCommand> _returnToBaseModelCommand;
        public ICommand ReturnToBaseModelCommand => _returnToBaseModelCommand.Value;
        
        protected Lazy<DelegateCommand> _loadModelCommand;
        public ICommand LoadModelCommand => _loadModelCommand.Value;

        protected Lazy<DelegateCommand> _saveModelCommand;
        public ICommand SaveModelCommand => _saveModelCommand.Value;

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
                ResetPB();
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

        private void ResetPB()
        {
            ProgressProcent = 0;
            IsIndeterminate = false;
        }


        public MasterViewModel()
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
        private Model3DGroup LoadTriangleMesh(string bufer_PATH)
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
                //var mat = new MaterialGroup();
                //mat.Children.Add(new DiffuseMaterial(Brushes.Aqua));
                //mat.Children.Add(new SpecularMaterial(Brushes.Blue, 80));
                var resultMesh = new GeometryModel3D()
                {
                    Geometry = mesh,
                    Material = new DiffuseMaterial(new SolidColorBrush(
                        Color.FromRgb(
                            (byte)rnd.Next(256),
                            (byte)rnd.Next(256),
                            (byte)rnd.Next(256))))
                };
                resultmodels.Children.Add(resultMesh);
                resultmodels.Children.Add(wireframe);
            }
            return resultmodels;
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
                    foreach (int[] tri_list in group_tri_sets)
                    {
                        SaveTriangleMeshByIdInFile(model, tri_list, bufer_group_PATH, writeOptions);
                        resultmodels.Children.Add(LoadTriangleMesh(bufer_group_PATH));
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
            IProgress<int> progress = new Progress<int>((p) => ProgressProcent = p);
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

        private void ResetModel(object param)
        {
            IsIndeterminate = true;
            renderModel = new DMesh3(baseModel);
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
            StandardMeshWriter.WriteFile(
                filename,
                new List<WriteMesh>() { new WriteMesh(renderModel) },
                writeOptions);
        }

        private void Render()
        {
            renderModel = renderModel == null ? new DMesh3(baseModel) : renderModel;
            OnPropertyChanged(nameof(MeshValid));
            MainMesh = ConvertToModel3D(renderModel);
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
