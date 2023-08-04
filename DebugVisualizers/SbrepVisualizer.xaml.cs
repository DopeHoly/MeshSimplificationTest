using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace MeshSimplificationTest.SBRepVM
{
    /// <summary>
    /// Interaction logic for SbrepVisualizer.xaml
    /// </summary>
    public partial class SbrepVisualizerViewer : Window
    {
        public Model3D Model { get; set; }
        public SbrepVisualizerViewer()
        {
            InitializeComponent();
        }
    }
}
