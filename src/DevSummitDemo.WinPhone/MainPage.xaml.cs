using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using DevSummitDemo.Resources;
using Esri.ArcGISRuntime.Toolkit.Controls;
using Esri.ArcGISRuntime.Controls;

namespace DevSummitDemo
{
	public partial class MainPage : PhoneApplicationPage
	{
        private MainPageVM m_viewModel;
		// Constructor
		public MainPage()
		{
			InitializeComponent();

            m_viewModel = this.Resources["vm"] as MainPageVM;
		}

        private void OnTemplatePicked(object sender, TemplatePicker.TemplatePickedEventArgs e)
        {
            m_viewModel.OnTemplatePickedCommand.Execute(e);
        }

        private void OnSyncClick(object sender, EventArgs e)
        {
            m_viewModel.OnSyncRequestedCommand.Execute(null);
        }

        private void OnProvisionDataClick(object sender, EventArgs e)
        {
            m_viewModel.OnProvisionOfflineDataRequested.Execute(null);
        }

        private void OnExtentChanged(object sender, EventArgs e)
        {
            var MapView = (MapView)sender;
            m_viewModel.CurrentExtent = MapView.Extent;
            m_viewModel.CurrentScale = MapView.Scale;
        }
	}
}