using g3;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private const string MODEL_PATH = @"C:\Users\User\Downloads\test2.stl";
        private const string bufer_PATH = @"C:\GitProjects\bufer.obj";
        private const string debugBufer_PATH = @"C:\GitProjects\debugbufer.obj";

        private DMesh3 baseModel { get; set; }
        private DMesh3 renderModel { get; set; }
        public RemeshTool RemeshTool { get; set; }

        public int TriangleCount { get; set; }
        public int TriangleFullCount { get; set; }

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
            RemeshTool.SetGroupByNormal(baseModel);
            RemeshTool.ExportByGroup(baseModel);
            TriangleFullCount = baseModel.TriangleCount;
        }

        private Model3D ConvertToModel3D(DMesh3 model)
        {
            Model3DGroup resultmodels = null;
            try
            {
                WriteOptions writeOptions = new WriteOptions();
                writeOptions.bPerVertexColors = true;
                StandardMeshWriter.WriteFile(
                    bufer_PATH,
                    new List<WriteMesh>() { new WriteMesh(model) },
                    writeOptions);

                //Import 3D model file
                ModelImporter import = new ModelImporter();

                //Load the 3D model file
                var models = import.Load(bufer_PATH);
                var geometryModel = models.Children[0] as GeometryModel3D;
                var mesh = geometryModel.Geometry as MeshGeometry3D;
                var triangleMesh = MeshExtensions.ToWireframe(mesh, 0.01);
                DiffuseMaterial wireframe_material =
                    new DiffuseMaterial(Brushes.Black);
                var resultTriangleMesh = new GeometryModel3D()
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
                    Material = mat
                };
                resultmodels = new Model3DGroup();
                resultmodels.Children.Add(resultMesh);
                resultmodels.Children.Add(resultTriangleMesh);
                
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
                renderModel = await RemeshTool.CalculateAsync(baseModel, Token.Token, progress);
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
