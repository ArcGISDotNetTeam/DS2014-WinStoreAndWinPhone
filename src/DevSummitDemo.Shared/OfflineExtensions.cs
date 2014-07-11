using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.Tasks.Geoprocessing;
using Esri.ArcGISRuntime.Tasks.Offline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Windows.Storage;
#if NETFX_CORE
using Windows.UI.Xaml;
#endif

namespace DevSummitDemo
{
    public static class OfflineExtensions
    {
        public static async Task<ArcGISLocalTiledLayer> TakeOffline(
            this ArcGISTiledMapServiceLayer onlineTiledLayer,   // The layer to take offline
            Envelope extent,                                    // The extent defining the area to take offline
            double maxScale,                                    // The scale below which to retrieve map tiles                                 
            Action<string> onStatusUpdate,                      // Callback to receive status updates
            string folderName = "Tiles")                        // Name of the folder that will store offline tiles
        {
            onStatusUpdate("Submitting export tile job...");

            // Specify the export options - tile format, extent, scale levels
            var generateOptions = new GenerateTileCacheParameters()
            {
                Format = ExportTileCacheFormat.TilePackage, // can export to compact cache or tile package
                GeometryFilter = extent,
                MaxScale = maxScale,    // leaving min or max unspecified will export all tiles down/up
                // to min/max scale level
            };

            // Create folder to hold downloaded tiles
            StorageFolder downloadLocation = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "Tiles", CreationCollisionOption.OpenIfExists);

            // Specify options for downloading tiles - location and whether to overwrite
            var downloadOptions = new DownloadTileCacheParameters(downloadLocation)
            {
                OverwriteExistingFiles = true
            };

            var task = new ExportTileCacheTask(new Uri(onlineTiledLayer.ServiceUri));
            var downloadResult = await task.GenerateTileCacheAndDownloadAsync(
                generateOptions,            // Options for cache generation
                downloadOptions,            // Options for cache download
                TimeSpan.FromSeconds(3),    // Frequency of status checks
                CancellationToken.None,     // Token to manage cancellation

                // Callback to handle status updates during tile cache generation
                new Progress<ExportTileCacheJob>(
                    (job) => onStatusUpdate(getTileCacheGenerationStatusMessage(job))),

                // Callback to handle status updates during tile cache download
                new Progress<ExportTileCacheDownloadProgress>(
                    (progress) => onStatusUpdate(getDownloadStatusMessage(progress))));

            onStatusUpdate("Download tiles complete");

            return new ArcGISLocalTiledLayer(downloadResult.OutputPath);
        }

        public static async Task<Geodatabase> TakeFeaturesOffline(
            string featureServiceUrl,       // Feature service containing layers to take offline
            IEnumerable<int> layerIDs,      // IDs of layers to take offline
            Envelope extent,                // Extent to get features within
            Action<string> onStatusUpdate)  // Status update callback
        {
            onStatusUpdate("Submitting request for geodatabase generation");

            // Create parameters for geodatabase generation
            var gdbParameters = new GenerateGeodatabaseParameters(
                layerIDs,                                       // Layers to take offline
                extent)                                         // Extent to take offline
            {
                SyncModel = SyncModel.PerGeodatabase,           // Stay synced with the whole geodatabase
                OutSpatialReference = extent.SpatialReference   // Get features in the same s-ref as the export extent
            };

            // Create the task for generating the geodatabase
            GeodatabaseSyncTask gdbTask = new GeodatabaseSyncTask(new Uri(featureServiceUrl));

            // Use TaskCompletionSource to make geodatabase geneation awaitable
            TaskCompletionSource<GeodatabaseStatusInfo> tcs = new TaskCompletionSource<GeodatabaseStatusInfo>();

            #region Callback declarations

            // Callback to handle operation completion
            Action<GeodatabaseStatusInfo, Exception> onGenerateCompleted = (status, ex) =>
            {
                if (ex != null)
                    tcs.TrySetException(ex);    // Return to awaiter with error
                else
                    tcs.TrySetResult(status);   // Return to awaiter with result
            };

            // Callback to handle operation status updates
            Action<GeodatabaseStatusInfo> onGenerateUpdate = (status) =>
            {
                onStatusUpdate(string.Format("Geodatabase generation status: {0}", status.Status));
            };
            var onGenerateProgress = new Progress<GeodatabaseStatusInfo>(onGenerateUpdate);

            #endregion

            // Do the geodatabase generation
            var result = await gdbTask.GenerateGeodatabaseAsync( // Returns token for resuming later
                gdbParameters,              // Parameters for the operation
                onGenerateCompleted,        // Callback to handle operation completion
                TimeSpan.FromSeconds(3),    // Interval to check status
                onGenerateProgress,         // Callback to handle operation status updates
                CancellationToken.None);    // Token to handle cancellation

            // Wait for the geodatabase generation operation to complete
            var generateResult = await tcs.Task;

            // Check whether operation completed successfully
            if (generateResult.Status != GeodatabaseSyncStatus.Completed)
            {
                // Operation was unsuccessful - report failure and skip download
                onStatusUpdate("Failed to generate geodatabase");
                return null;
            }

            // Report success and continue to downloading
            onStatusUpdate("Geodatabase generation complete.  Downloading geodatabase...");

            // Download the geodatabase
            var client = new ArcGISHttpClient();
            var response = await client.GetAsync(generateResult.ResultUri);

            // Write the download to disk
            string geodatabasePath = null;
            if (response.IsSuccessStatusCode)
            {
                // Create or open a sub-folder to contain the database
                var targetFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Features", CreationCollisionOption.OpenIfExists);

                // create database name based on the service
                featureServiceUrl = featureServiceUrl.Remove(featureServiceUrl.LastIndexOf("/FeatureServer"));
                string gdbName = featureServiceUrl.Substring(featureServiceUrl.LastIndexOf("/") + 1) + ".gdb";

                // Create a file for the database
                StorageFile file = await targetFolder.CreateFileAsync(gdbName, CreationCollisionOption.ReplaceExisting);
                geodatabasePath = file.Path;

                // Write the downloaded database to the file
                using (var stream = await file.OpenStreamForWriteAsync())
                {
                    await response.Content.CopyToAsync(stream);
                    await stream.FlushAsync();
                }
            }
            else
            {
                // Download failed - report and exit
                onStatusUpdate(string.Format("Geodatabase download failed.  Response code '{0}'", response.StatusCode));
                return null;
            }

            // Report download completion
            onStatusUpdate("Geodatabase download complete");

            // Get the geodatabase
            return await Geodatabase.OpenAsync(geodatabasePath);
        }

        public static async Task<GeodatabaseStatusInfo> Sync(
    this Geodatabase gdb,           // The geodatabase to sync
    string featureServiceUrl,       // URL of the feature service to sync against
    Action<string> onStatusUpdate)  // Status update callback
        {
            onStatusUpdate("Submitting sync request");

            // Initialize geodatabase task and sync parameters
            GeodatabaseSyncTask gdbTask = new GeodatabaseSyncTask(new Uri(featureServiceUrl));
            var syncParameters = new SyncGeodatabaseParameters(gdb);

            // Use TaskCompletionSource to make the sync operation awaitable
            TaskCompletionSource<GeodatabaseStatusInfo> tcs = new TaskCompletionSource<GeodatabaseStatusInfo>();

            // Callback to handle completion of sync operation
            Action<GeodatabaseStatusInfo, Exception> onSyncCompleted = (status, ex) =>
            {
                if (ex != null) // Error occurred
                {
                    tcs.TrySetException(ex); // Return to awaiter with error
                }
                else
                {
                    tcs.TrySetResult(status); // Return to awaiter with result
                }
            };

            // Callback to handle completion of changes being uploaded to server
            Action<UploadResult> onDeltaUploadCompleted = (uploadResult) =>
            {
                onStatusUpdate("Sync status: Uploading changes completed");
            };

            // Callback to handle status updates
            var onSyncStatusChange = new Progress<GeodatabaseStatusInfo>(
                    (status) => { onStatusUpdate(string.Format("Sync status: {0}", status.Status)); });

            // Execute the sync operation
            var result = await gdbTask.SyncGeodatabaseAsync(
                syncParameters,             // Operation parameters 
                gdb,                        // Geodatabase to sync - take directly from layer              
                onSyncCompleted,            // Callback to handle completion of sync operation                
                onDeltaUploadCompleted,     // Callback for completion of upload
                TimeSpan.FromSeconds(3),    // Interval to check sync status
                onSyncStatusChange,         // Callback for status updates                
                CancellationToken.None);    // Token for handling cancellation

            // Await TaskCompletionSource's task to wait until sync operation is complete
            var syncResult = await tcs.Task;

            // Report completion or failure to status callback
            if (syncResult.Status != GeodatabaseSyncStatus.Completed)
            {
                onStatusUpdate(string.Format("Synchronization failed. Completion status: {0}", syncResult.Status));
            }
            else
            {
                onStatusUpdate("Sync complete");
            }

            return syncResult;
        }
















        public static void Replace(this LayerCollection layers, Layer layerToRemove, Layer layerToAdd)
        {
            int index = layers.IndexOf(layerToRemove);
            if (index < 0)
                throw new Exception("Layer not found in the target LayerCollection");

            layers.Insert(index, layerToAdd);
            layers.Remove(layerToRemove);
        }

        public static readonly DependencyProperty OriginalServiceUriProperty = DependencyProperty.RegisterAttached(
            "OriginalServiceUri", typeof(string), typeof(FeatureLayer), null);

        public static void SetOriginalServiceUri(FeatureLayer layer, string value)
        {
            layer.SetValue(OriginalServiceUriProperty, value);
        }

        public static string GetOriginalServiceUri(FeatureLayer layer)
        {
            return (string)layer.GetValue(OriginalServiceUriProperty);
        }

        private static string getDownloadStatusMessage(ExportTileCacheDownloadProgress downloadProgress)
        {
            return string.Format("Downloading file {0} of {1}...\n{2:P0} complete\n" +
                "Bytes read: {3}", downloadProgress.FilesDownloaded, downloadProgress.TotalFiles, downloadProgress.ProgressPercentage,
                downloadProgress.CurrentFileBytesReceived);
        }

        private static string getTileCacheGenerationStatusMessage(ExportTileCacheJob job)
        {
            var text = string.Format("Job Status: {0}\n\nMessages:\n=====================\n", job.Status);
            foreach (GPMessage message in job.Messages)
            {
                text += string.Format("Message type: {0}\nMessage: {1}\n--------------------\n",
                    message.MessageType, message.Description);
            }
            return text;
        }
    }
}
