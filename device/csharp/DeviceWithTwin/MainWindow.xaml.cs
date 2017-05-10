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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System.Windows.Threading;
using System.Diagnostics;

namespace DeviceWithTwin
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        string azureIoTHubCS = "<< Azure IoT Hub Connection String for Device Id >>";

        Models.DesiredProperties desiredProperties;
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            desiredProperties = new Models.DesiredProperties();
            this.desiredPropsPane.DataContext = desiredProperties;
            tbIoTHubCS.Text = azureIoTHubCS;
        }
        TrackingCar trackingCarWindow;
        Microsoft.Azure.Devices.Client.DeviceClient deviceClient;
        string buttonIoTHubLabelConnect = "Connect";
        string buttonIoTHubLabelDisconnect = "Disconnect";

        string deviceMethodName_ChargeBattery = "ChargeBattery";
        string deviceMethodName_Reboot = "Reboot";

        DispatcherTimer uploadTimer;
        bool isConnected = false;

        private async void buttonIoTHub_Click(object sender, RoutedEventArgs e)
        {
            if (deviceClient == null)
            {
                deviceClient = Microsoft.Azure.Devices.Client.DeviceClient.CreateFromConnectionString(azureIoTHubCS, Microsoft.Azure.Devices.Client.TransportType.Mqtt);
                await deviceClient.SetDesiredPropertyUpdateCallback(DesiredPropertyUpdateCallback, this);
                await deviceClient.SetMethodHandlerAsync(deviceMethodName_ChargeBattery, ChargeBatteryMethod, this);
                await deviceClient.SetMethodHandlerAsync(deviceMethodName_Reboot, RebootMethod, this);
            }
            if (buttonIoTHub.Content.ToString() == buttonIoTHubLabelConnect)
            {
                await deviceClient.OpenAsync();
                tbIoTHubStatus.Text = "Connected";
                var twin = await deviceClient.GetTwinAsync();
                var json = twin.Properties.Desired.ToJson();
                this.desiredProperties.Deserialize(json);
                buttonIoTHub.Content = buttonIoTHubLabelDisconnect;
                isConnected = true;
                trackingCarWindow = new TrackingCar(twin.DeviceId, this.desiredProperties,deviceClient);
                trackingCarWindow.Show();
                this.reportedPropsPane.DataContext = trackingCarWindow.ThingsCar;
            }
            else if (buttonIoTHub.Content.ToString() == buttonIoTHubLabelDisconnect)
            {
                if (uploadTimer != null && uploadTimer.IsEnabled)
                {
                    uploadTimer.Stop();
                }
                await deviceClient.CloseAsync();
                isConnected = false;
                tbIoTHubStatus.Text = "Disconnected";
                buttonIoTHub.Content = buttonIoTHubLabelConnect;
            }
        }

        private Task<MethodResponse> RebootMethod(MethodRequest methodRequest, object userContext)
        {
            return Task.Run(() =>
            {
                Debug.WriteLine("Called:{0},Data:{1}", methodRequest.Name, methodRequest.DataAsJson);
                return new MethodResponse(Encoding.UTF8.GetBytes("{\"Status\":\"Rebooting\"}"),0);
            });
        }

        private Task<MethodResponse> ChargeBatteryMethod(MethodRequest methodRequest, object userContext)
        {
            return Task.Run(() =>
            {
                Debug.WriteLine("Called:{0},Data:{1}", methodRequest.Name, methodRequest.DataAsJson);
                return new MethodResponse(0);
            });
        }

        private Task DesiredPropertyUpdateCallback(TwinCollection desiredProperties, object userContext)
        {
            // TODO:
            // open car view and start to telemetry
            return Task.Run(() =>
            {
                var json = desiredProperties.ToJson();
                Debug.WriteLine("DesiredPropetries:{0}", json);
                this.desiredProperties.Deserialize(json);
            });
        }

        private async void buttonReport_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected)
            {
                var reportedProps = new Models.ReportedProperties()
                {
                    BatteryLevel = trackingCarWindow.ThingsCar.BatteryLevel,
                    RealLatitude = trackingCarWindow.ThingsCar.Latitude,
                    RealLongitude = trackingCarWindow.ThingsCar.Longitude,
                    Status = trackingCarWindow.ThingsCar.Status
                };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(reportedProps);

                var patch = Newtonsoft.Json.JsonConvert.DeserializeObject<TwinCollection>(json);
                await deviceClient.UpdateReportedPropertiesAsync(patch);
            }
        }
    }
}
