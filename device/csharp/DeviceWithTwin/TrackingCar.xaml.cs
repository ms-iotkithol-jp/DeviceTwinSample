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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DeviceWithTwin
{
    /// <summary>
    /// TrackingCar.xaml の相互作用ロジック
    /// </summary>
    public partial class TrackingCar : Window
    {
        public TrackingCar()
        {
            InitializeComponent();
        }

        Models.ThingsCar thingsCar;

        public TrackingCar(string deviceId, Models.DesiredProperties dp, Microsoft.Azure.Devices.Client.DeviceClient client)
        {
            InitializeComponent();
            thingsCar = new Models.ThingsCar(deviceId);
            double posx, posy;
            thingsCar.GetPosition(dp.Latitude, dp.Longitude, out posx, out posy);
            thingsCar.PosX = posx;
            thingsCar.PosY = posy;
            deviceClient = client;
            TelemetryCycle = dp.TelemetryCycle;

            this.Loaded += TrackingCar_Loaded;
        }

        private void TrackingCar_Loaded(object sender, RoutedEventArgs e)
        {
            canvasTracking.DataContext = thingsCar;
            posiitonPane.DataContext = thingsCar;
        }

        public Models.ThingsCar ThingsCar { get { return thingsCar; } }

        private Point lastMousePoint;
        private bool mouseOnCar = false;
        private DateTime? lastMouseMoveTime;

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            mouseOnCar = true;
            lastMousePoint = e.GetPosition(canvasTracking);
            lastMouseMoveTime = DateTime.Now;
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseOnCar)
            {
                var current = e.GetPosition(canvasTracking);
                double dx = current.X - lastMousePoint.X;
                double dy = current.Y - lastMousePoint.Y;
                thingsCar.UpdatePosition(thingsCar.PosX + dx, thingsCar.PosY + dy, new Point(thingsCar.PosX, thingsCar.PosY),lastMouseMoveTime.Value);
                lastMousePoint = current;
                lastMouseMoveTime = DateTime.Now;
            }
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            mouseOnCar = false;
            lastMouseMoveTime = null;
            thingsCar.Status = Models.ThingsCarStatus.Stopping;
        }

        private void Image_MouseLeave(object sender, MouseEventArgs e)
        {
            mouseOnCar = false;
            lastMouseMoveTime = null;
            thingsCar.Status = Models.ThingsCarStatus.Stopping;
        }

        private Microsoft.Azure.Devices.Client.DeviceClient deviceClient;

        DispatcherTimer uploadTimer;
        string buttonTrackingLabelStart = "Tracking Start";
        string buttonTrackingLabelStop = "Tracking Stop";
        public int TelemetryCycle { get; set; }
        private void buttonTracking_Click(object sender, RoutedEventArgs e)
        {
            if (uploadTimer == null)
            {
                uploadTimer = new DispatcherTimer();
                uploadTimer.Tick += UploadTimer_Tick;
            }
            if (buttonTracking.Content.ToString() == buttonTrackingLabelStart)
            {
                uploadTimer.Interval = TimeSpan.FromMilliseconds(TelemetryCycle);
                lock (thingsCar)
                {
                    thingsCar.Tracking = true;
                }
                uploadTimer.Start();
                buttonTracking.Content = buttonTrackingLabelStop;
            }
            else if (buttonTracking.Content.ToString()==buttonTrackingLabelStop)
            {
                lock (thingsCar)
                {
                    thingsCar.Tracking = false;
                }
                uploadTimer.Stop();
                buttonTracking.Content = buttonTrackingLabelStart;
            }
        }

        private void UploadTimer_Tick(object sender, EventArgs e)
        {
            var messages = new List<Microsoft.Azure.Devices.Client.Message>();
            lock (thingsCar)
            {
                if (thingsCar.Temetries.Count > 0)
                {
                    foreach (var t in thingsCar.Temetries)
                    {
                        messages.Add(new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(t))));
                    }
                    thingsCar.Temetries.Clear();
                }
            }
            if (messages.Count > 0)
            {
                deviceClient.SendEventBatchAsync(messages);
            }
        }

        private double goalX;
        private double goalY;

        private void MapImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var point = e.GetPosition(canvasTracking);
            Canvas.SetLeft(imgGoal, point.X - imgGoal.ActualWidth / 2);
            Canvas.SetTop(imgGoal, point.Y - imgGoal.ActualHeight / 2);
            if(imgGoal.Visibility== Visibility.Hidden)
            {
                imgGoal.Visibility = Visibility.Visible;
            }
            thingsCar.MoveTo(point);
        }

        private void buttonCharge_Click(object sender, RoutedEventArgs e)
        {
            thingsCar.Charge();
        }
    }
}
