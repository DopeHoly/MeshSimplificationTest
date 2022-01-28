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
    public class MasterViewModel : INotifyPropertyChanged, IMeshHost
    {
        public IMeshWidget RemeshWidget { get; set; }
        public TrulyObservableCollection<MeshViewModel> ModelCollection { get; set; }
        public ObservableCollection<Model3D> viewCollection { get; set; }
        
        private MeshViewModel _selectMesh;
        public MeshViewModel SelectMesh
        {
            get => _selectMesh;
            set
            {
                _selectMesh = value;
                if (RemeshWidget.Visible)
                {
                    RemeshWidget.SetModel(SelectMesh?.Model);
                }
                else
                {
                    Render();
                }
            }
        }

        private Model3DGroup _tmpmainMesh;
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

        protected Lazy<DelegateCommand> _saveModelCommand;
        public ICommand SaveModelCommand => _saveModelCommand.Value;

        protected Lazy<DelegateCommand> _IntersectionCommand;
        public ICommand IntersectionCommand => _IntersectionCommand.Value;

        protected Lazy<DelegateCommand> _CallRemeshCommand;
        public ICommand CallRemeshCommand => _CallRemeshCommand.Value;

        public MasterViewModel()
        {
            ModelCollection = new TrulyObservableCollection<MeshViewModel>();
            viewCollection = new ObservableCollection<Model3D>();
            _loadModelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(LoadModel));
            _saveModelCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(SaveModel));
            _IntersectionCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(IntersectionEx));
            _CallRemeshCommand = new Lazy<DelegateCommand>(() =>
                new DelegateCommand(CallRemeshCommandEx, CallRemeshCommandCanEx));

            ModelCollection.CollectionChanged += ModelCollection_CollectionChanged;

            RemeshWidget = new RemeshWidget();
            RemeshWidget.SetParent(this);

        }

        private void ModelCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(!RemeshWidget.Visible)
                Render();
        }

        private void AddModel(string path)
        {
            var model = StandardMeshReader.ReadMesh(path);
            var model3d = new MeshViewModel()
            {
                Model = model,
                Name = Path.GetFileName(path),
                Visible = true
            };
            model3d.ChangeColor(GetColor(ModelCollection.Count + 1));
            ModelCollection.Add(model3d);
        }
        private void AddModel(DMesh3 mesh, string name)
        {
            if (mesh == null) return;
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
                if (!item.Visible) continue;
                var model = item.GetModel(GetColor(cnt)); 
                newMesh.Children.Add(item.Model_3D);
                if (item.Equals(SelectMesh))
                {
                    newMesh.Children.Add(item.Warframe);
                }
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
            //Render();
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
                new List<WriteMesh>() { new WriteMesh(SelectMesh.Model) },
                writeOptions);
        }

        private void IntersectionEx(object obj)
        {
            try
            {
                var a = ModelCollection[0];
                var b = ModelCollection[1];
                var booleanTool = new BooleanToolNet3dBool();
                AddModel(booleanTool.Intersection(a.Model, b.Model), $"{a.Name} - {b.Name}");
                SelectMesh = ModelCollection[ModelCollection.Count - 1];
                //SaveModel(null);
            }
            catch(Exception ex) 
            {
                ;
            }
        }

        private void CallRemeshCommandEx(object obj)
        {
            if (RemeshWidget.Visible)
            {
                RemeshWidget.Close();
                ResetView();
            }
            else
            {
                _tmpmainMesh = MainMesh.Clone();
                RemeshWidget.SetModel(SelectMesh.Model);
                RemeshWidget.Visible = true;
            }
        }
        private bool CallRemeshCommandCanEx(object obj)
        {
            return SelectMesh != null;
        }
        public void Render(DMesh3 model)
        {
            var newMesh = new Model3DGroup();
            var item = new MeshViewModel()
            {
                Model = model,
                Visible = true,
                Name = SelectMesh.Name
            };
            newMesh.Children.Add(item.Model_3D);
            newMesh.Children.Add(item.Warframe);
            MainMesh = newMesh;
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

        public void SetModel(DMesh3 render)
        {
            SelectMesh.Model = render;
        }

        public void ResetView()
        {            
            MainMesh = _tmpmainMesh.Clone();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}