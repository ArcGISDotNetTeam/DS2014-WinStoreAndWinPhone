using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Location;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.Toolkit.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using DevSummitDemo;

namespace DevSummitDemo
{
	public class MainPageVM : BaseViewModel
	{
		public MainPageVM()
		{
			LocationDisplay = new LocationDisplay();
			Editor = new Editor();
			OnTemplatePickedCommand = new DelegateCommand((template) => { OnTemplatePicked((TemplatePicker.TemplatePickedEventArgs)template); });
			OnDataFormAppliedCommand = new DelegateCommand(() => { });
			OnSyncRequestedCommand = new DelegateCommand(OnSyncRequested);

            IdentityManager.Current.ChallengeMethod = OnChallenge;
            OnProvisionOfflineDataRequested = new DelegateCommand(provisionOfflineData);
        }

        private async Task<IdentityManager.Credential> OnChallenge(IdentityManager.CredentialRequestInfo arg)
        {
            return await IdentityManager.Current.GenerateCredentialAsync(arg.ServiceUri, UserName, Password);
        }

		#region Editing

		public ICommand OnTemplatePickedCommand { get; private set; }

		private async void OnTemplatePicked(TemplatePicker.TemplatePickedEventArgs e)
		{
			Geometry geometry = null;
			try
			{
				if (e.Layer.FeatureTable.ServiceInfo.GeometryType == Esri.ArcGISRuntime.Geometry.GeometryType.Point)
					geometry = await Editor.RequestShapeAsync(DrawShape.Point);
				else
					geometry = await Editor.RequestShapeAsync(DrawShape.Polygon);
			}
			catch(OperationCanceledException)
			{
				return;
			}
			var row = e.Layer.FeatureTable.CreateNew();
			row.Geometry = geometry;
			row.CopyFrom(e.FeatureTemplate.Prototype.Attributes);
			await e.Layer.FeatureTable.AddAsync(row);
		}
	
		public ICommand OnDataFormAppliedCommand { get; private set; }

		#endregion Editing

		#region Syncing data
		public ICommand OnSyncRequestedCommand { get; private set; }

		private async void OnSyncRequested()
		{
			IsBusy = true;
            if (HasOfflineData)
                await syncOfflineTables();
            else
                await SyncOnlineTables();
            IsBusy = false;
		}

		private async Task SyncOnlineTables()
		{
			foreach (var table in Map.Layers.OfType<FeatureLayer>().Select(f => f.FeatureTable).OfType<Esri.ArcGISRuntime.Data.GeodatabaseFeatureServiceTable>())
			{
				if (table.HasEdits)
				{
					try
					{
						await table.ApplyEditsAsync(false); //Upload edits
					}
					catch { /* TODO: Handle errors... */ }
				}
				table.RefreshFeatures(true); //Download changes
			}
		}

        private bool m_IsBusy;

		public bool IsBusy
		{
            get { return m_IsBusy; }
            set { m_IsBusy = value; OnPropertyChanged(); }
		}
		#endregion Syncing data

		#region MapView Properties

		private Map m_Map;

		public Map Map
		{
			get { return m_Map; }
			set { m_Map = value; OnPropertyChanged(); }
		}

		private Editor m_Editor;

		public Editor Editor
		{
			get { return m_Editor; }
			set { m_Editor = value; }
		}		

		private LocationDisplay m_LocationDisplay;

		public LocationDisplay LocationDisplay
		{
			get { return m_LocationDisplay; }
			set { m_LocationDisplay = value; OnPropertyChanged(); }
		}

		#endregion MapView Properties

        public ICommand OnProvisionOfflineDataRequested { get; private set; }

        public bool HasOfflineData { get; private set; }

        private string m_status;
        public string Status
        {
            get { return m_status; }
            set
            {
                if (m_status != value)
                {
                    m_status = value;
                    OnPropertyChanged();
                }
            }
        }


        private Envelope m_currentExtent;
        public Envelope CurrentExtent
        {
            get { return m_currentExtent; }
            set
            {
                if (m_currentExtent != value)
                {
                    m_currentExtent = value;
                    OnPropertyChanged();
                }
            }
        }

        private double m_currentScale;
        public double CurrentScale
        {
            get { return m_currentScale; }
            set
            {
                if (m_currentScale != value)
                {
                    m_currentScale = value;
                    OnPropertyChanged();
                }
            }
        }
        public string UserName { get; set; }
        public string Password { get; set; }

        private async void provisionOfflineData(object parameter)
        {
            IsBusy = true;
            double exportScale = CurrentScale * 2;
            Envelope exportExtent = CurrentExtent.Expand(1.5);

            try
            {
                #region Take tiles offline

                foreach (var onlineTiledLayer in Map.Layers.OfType<ArcGISTiledMapServiceLayer>())
                {
                    // Create the layer with the downloaded tiles
                    ArcGISLocalTiledLayer localTiledLayer = await onlineTiledLayer.TakeOffline(
                        exportExtent, exportScale, (status) => { Status = status; });

                    // Replace online tiled layer with the local one
                    Map.Layers.Replace(onlineTiledLayer, localTiledLayer);
                }
                #endregion
            }
            catch (Exception ex)
            {
                Status = string.Format("Error occurred while taking tiled layer offline:\n{0}", ex.Message);
            }

            try
            {
                #region Take geodatabase (features) offline

                // Get the URL of the feature service and the IDs of the layers to take offline
                List<int> featureLayerIDs = new List<int>();
                string featureServiceUrl = null;
                foreach (var featureLayer in Map.Layers.OfType<FeatureLayer>())
                {
                    // Get the layer ID and add to the list
                    var table = featureLayer.FeatureTable as GeodatabaseFeatureServiceTable;
                    var layerID = table.ServiceInfo.ID;
                    featureLayerIDs.Add(layerID);

                    // Get the URL of the feature service - this assumes that all feature layers
                    // in the map come from the same service
                    if (featureServiceUrl == null)
                    {
                        featureServiceUrl = table.ServiceUri.Remove(table.ServiceUri.LastIndexOf(layerID.ToString()));
                    }
                }

                // Generate and download the geodatabase
                var geodatabase = await OfflineExtensions.TakeFeaturesOffline(
                    featureServiceUrl,
                    featureLayerIDs,
                    exportExtent,
                    (status) => { Status = status; });

                // Swap out feature layers with online feature tables for feature layers with
                // tables that point to the local geodatabase
                for (int i = 0; i < Map.Layers.Count; i++)
                {
                    if (!(Map.Layers[i] is FeatureLayer)) // only interested in feature layers
                        continue;

                    // Get the online feature table for the layer
                    var onlineFeatureLayer = (FeatureLayer)Map.Layers[i];
                    var table = onlineFeatureLayer.FeatureTable as GeodatabaseFeatureServiceTable;

                    // Get the feature table in the generated geodatabase with the ID that matches
                    // that of the online feature table
                    var localTable = geodatabase.FeatureTables.Where(
                        t => t.ServiceInfo.ID == onlineFeatureLayer.FeatureTable.ServiceInfo.ID).First();

                    // Create a feature layer with the local table
                    FeatureLayer localFeatureLayer = new FeatureLayer(localTable);
                    OfflineExtensions.SetOriginalServiceUri(localFeatureLayer, featureServiceUrl);

                    // Show the "local" feature layer in the map
                    Map.Layers.Replace(onlineFeatureLayer, localFeatureLayer);
                }

                #endregion
            }
            catch (Exception ex)
            {
                Status = string.Format("Error occurred while taking feature layer offline:\n{0}", ex.Message);
            }
            HasOfflineData = true;
            IsBusy = false;
        }

        private async Task syncOfflineTables()
        {
            try
            {
                // Get the first feature layer - assume that all feature layers are
                // using the same geodatabase
                var featureLayer = Map.Layers.OfType<FeatureLayer>().First();

                // Get feature service URL
                var featureServiceUrl = OfflineExtensions.GetOriginalServiceUri(featureLayer);

                // Execute sync operation
                var result = await featureLayer.FeatureTable.Geodatabase.Sync(
                    featureServiceUrl,
                    (status) => { Status = status; });
            }
            catch (Exception ex)
            {
                Status = string.Format("Error occurred during synchronization.\nException message: {0}", ex.Message);
            }
        }
    }
}
