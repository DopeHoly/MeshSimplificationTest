using g3;
using HelixToolkit.Wpf;
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
        private const string MODEL_PATH = @"C:\Users\menin\Documents\cilinder.obj";
        //private const string MODEL_PATH = @"C:\Users\User\Downloads\145U_14Part_withoutRound_withoutMerge_withoutCorrect.stl";
        private const string bufer_PATH = @"C:\Users\menin\Documents\bufer.obj";
        private const string debugBufer_PATH = @"C:\GitProjects\debugbufer.obj";

        private DMesh3 baseModel { get; set; }
        private DMesh3 renderModel { get; set; }

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

        public double EdgeLength { get; set; }
        public double SmoothSpeed { get; set; }
        public double Angle { get; set; }
        public int Iterations { get; set; }
        public bool EnableFlips { get; set; }
        public bool EnableCollapses { get; set; }
        public bool EnableSplits { get; set; }
        public bool EnableSmoothing { get; set; }
        public bool KeepAngle { get; set; }

        
        protected Lazy<DelegateCommand> _applyCommand;
        public ICommand ApplyCommand => _applyCommand.Value; 

        protected Lazy<DelegateCommand> _cancelCommand;
        public ICommand CancelCommand => _cancelCommand.Value; 
        
        protected Lazy<DelegateCommand> _returnToBaseModelCommand;
        public ICommand ReturnToBaseModelCommand => _returnToBaseModelCommand.Value;

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
        private CancellationToken token;
        public CancellationToken Token
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

            EdgeLength = 0.1;
            SmoothSpeed = 0.5;
            Iterations = 20;

            baseModel = LoadModel(MODEL_PATH);
            TriangleFullCount = baseModel.TriangleCount;
            Render();
        }

        private bool CancelCommandCanEx(object arg)
        {
            return Token != null && Token.CanBeCanceled;
        }

        private void CancelCommandEx(object obj)
        {
            if (Token.CanBeCanceled) Token.ThrowIfCancellationRequested();
        }

        private DMesh3 LoadModel(string path)
        {
            return StandardMeshReader.ReadMesh(path);
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
                var triangleMesh = MeshExtensions.ToWireframe(mesh, 0.1);
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

        private void ApplyCommandExecute()
        {
            CalculateAsync();
        }

        private async void CalculateAsync()
        {
            IProgress<int> progress = new Progress<int>((p) => ProgressProcent = p);
            Token = new CancellationToken();
            try
            {
                renderModel = await Task.Run(() => Calculate(progress, Token));
            }
            catch
            {

            }
            Render();
        }

        private DMesh3 Calculate(IProgress<int> progress, CancellationToken cancelToken)
        {
            var mesh = new DMesh3(baseModel);
            Remesher r = new Remesher(mesh);

            if (KeepAngle)
            {
                AxisAlignedBox3d bounds = mesh.CachedBounds;
                // construct mesh projection target
                DMesh3 meshCopy = new DMesh3(mesh);
                meshCopy.CheckValidity();
                DMeshAABBTree3 tree = new DMeshAABBTree3(meshCopy);
                tree.Build();
                MeshProjectionTarget target = new MeshProjectionTarget()
                {
                    Mesh = meshCopy,
                    Spatial = tree
                };
                MeshConstraints cons = new MeshConstraints();
                EdgeRefineFlags useFlags = EdgeRefineFlags.NoFlip;
                foreach (int eid in mesh.EdgeIndices())
                {
                    double fAngle = MeshUtil.OpeningAngleD(mesh, eid);
                    if (fAngle > Angle)
                    {
                        cons.SetOrUpdateEdgeConstraint(eid, new EdgeConstraint(useFlags));
                        Index2i ev = mesh.GetEdgeV(eid);
                        int nSetID0 = (mesh.GetVertex(ev[0]).y > bounds.Center.y) ? 1 : 2;
                        int nSetID1 = (mesh.GetVertex(ev[1]).y > bounds.Center.y) ? 1 : 2;
                        cons.SetOrUpdateVertexConstraint(ev[0], new VertexConstraint(true, nSetID0));
                        cons.SetOrUpdateVertexConstraint(ev[1], new VertexConstraint(true, nSetID1));
                    }
                }
                r.Precompute();
                r.SetExternalConstraints(cons);
                r.SetProjectionTarget(target);
            }

            r.EnableFlips = EnableFlips;
            r.EnableCollapses = EnableCollapses;
            r.EnableSplits = EnableSplits;

            //r.MinEdgeLength = 0.1f * EdgeLength;
            //r.MaxEdgeLength = 0.2f * EdgeLength;
            r.EnableSmoothing = EnableSmoothing;
            r.SmoothSpeedT = SmoothSpeed; 
            r.SetTargetEdgeLength(EdgeLength);

            if (cancelToken.IsCancellationRequested) return null;
            for (int k = 0; k < Iterations; ++k)
            {
                if (cancelToken.IsCancellationRequested) return null;
                int val = (int)Math.Truncate(((double)k / (double)Iterations) * 100);
                progress?.Report(val);
                r.BasicRemeshPass();
            }

            //r.PreventNormalFlips = true;
            //r.SetTargetEdgeLength(EdgeLength);
            //r.EnableSmoothing = false;
            //r.SetProjectionTarget(MeshProjectionTarget.Auto(renderModel));
            //r.SetExternalConstraints(new MeshConstraints());
            //MeshConstraintUtil.FixAllBoundaryEdges(r.Constraints, renderModel);

            //int set_id = 1;
            //int[][] group_tri_sets = FaceGroupUtil.FindTriangleSetsByGroup(renderModel);
            //foreach (int[] tri_list in group_tri_sets)
            //{
            //    MeshRegionBoundaryLoops loops = new MeshRegionBoundaryLoops(renderModel, tri_list);
            //    foreach (EdgeLoop loop in loops)
            //    {
            //        MeshConstraintUtil.ConstrainVtxLoopTo(r, loop.Vertices,
            //            new DCurveProjectionTarget(loop.ToCurve()), set_id++);
            //    }
            //}

            return mesh;
        }
        private void ResetModel(object param)
        {
            IsIndeterminate = true;
            renderModel = new DMesh3(baseModel);
            MainMesh = ConvertToModel3D(renderModel);
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
