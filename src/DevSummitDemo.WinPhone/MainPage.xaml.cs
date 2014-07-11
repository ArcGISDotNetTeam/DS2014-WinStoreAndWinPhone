using Esri.ArcGISRuntime.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace DevSummitDemo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MainPageVM m_viewModel;
        public MainPage()
        {
            try
            {
                this.InitializeComponent();

                m_viewModel = this.Resources["vm"] as MainPageVM;
            }
            catch (Exception ex)
            {
                string s = ex.Message;
            }
        }

        private void OnExtentChanged(object sender, EventArgs e)
        {
            var mapView = (MapView)sender;
            m_viewModel.CurrentExtent = mapView.Extent;
            m_viewModel.CurrentScale = mapView.Scale;
        }
    }
}
