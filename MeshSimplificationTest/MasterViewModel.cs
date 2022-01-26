using g3;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MeshSimplificationTest
{
    public class MeshViewModel
    {
        public DMesh3 Model { get; set; }
        public string Name { get; set; }
        public bool Visible { get; set; }

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
                triangleIndex.Add(triangle.a);
                triangleIndex.Add(triangle.b);
                triangleIndex.Add(triangle.c);
            }
            return new Mesh3D(points, triangleIndex);
        }
    }
    public class MasterViewModel : INotifyPropertyChanged
    {
        //public ObservableCollection<DMesh3> ModelCollection { get; set; }
        public ObservableCollection<MeshViewModel> ModelCollection { get; set; }
        public ObservableCollection<Model3D> viewCollection { get; set; }

        public DMesh3 SelectMesh { get; set; }

        private Model3DGroup _mainMesh;
        public Model3DGroup MainMesh
        {
            get => _mainMesh;
            set
            {
                _mainMesh = value;
                OnPropertyChanged();
            }
        }

        protected Lazy<DelegateCommand> _loadModelCommand;
        public ICommand LoadModelCommand => _loadModelCommand.Value;

        protected Lazy<DelegateCommand> _IntersectionCommand;
        public ICommand IntersectionCommand => _IntersectionCommand.Value;

        public MasterViewModel()
        {
            ModelCollection = new ObservableCollection<MeshViewModel>();
            viewCollection = new ObservableCollection<Model3D>();
            _loadModelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(LoadModel));
            _IntersectionCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(IntersectionEx));
        }
        private void AddModel(string path)
        {
            var model = StandardMeshReader.ReadMesh(path);
            ModelCollection.Add(new MeshViewModel()
            {
                Model = model,
                Name = Path.GetFileName(path),
                Visible = true
            });
        }
        private void AddModel(DMesh3 mesh, string name)
        {
            ModelCollection.Add(new MeshViewModel()
            {
                Model = mesh,
                Name = name,
                Visible = true
            });
        }

        private void Render()
        {
            if (ModelCollection == null && ModelCollection.Count < 1) return;
            var newMesh = new Model3DGroup();
            newMesh.Children.Clear();
            var cnt = 0;
            foreach (var item in ModelCollection)
            {
                newMesh.Children.Add(item.GetModel(GetColor(cnt)));
                ++cnt;
            }
            MainMesh = newMesh;
            //MainMesh = ConvertToModel3D(ModelCollection, MainMesh);
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
            var rnd = new Random();
            return Color.FromRgb(
                            (byte)rnd.Next(256),
                            (byte)rnd.Next(256),
                            (byte)rnd.Next(256));
        }
        private void LoadModel(object obj)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "3d Objects|*.obj;*.stl";
            ofd.Multiselect = true;
            if (ofd.ShowDialog() != true)
                return;
            // получаем выбранный файл
            var filenames = ofd.FileNames;
            foreach (var path in filenames)
            {
                AddModel(path);
            }
            Render();
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
                new List<WriteMesh>() { new WriteMesh(SelectMesh) },
                writeOptions);
        }

        private void IntersectionEx(object obj)
        {
            try
            {
                var a = ModelCollection[0];
                var b = ModelCollection[1];
                AddModel(BooleanTool.Intersection(a.Model, b.Model), $"{a.Name} - {b.Name}");
                SelectMesh = ModelCollection[ModelCollection.Count - 1]?.Model;
                SaveModel(null);
            }
            catch { }
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