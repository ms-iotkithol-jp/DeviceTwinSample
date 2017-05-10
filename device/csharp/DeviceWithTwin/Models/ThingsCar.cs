using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DeviceWithTwin.Models
{
    public class ThingsCar : INotifyPropertyChanged
    {
        private double posX;
        private double posY;
        private double latitude;
        private double longitude;
        private double batteryLevel;
        private ThingsCarStatus status;
        private bool tracking = false;

        private double deltaLatitude = -3.06192e-05;
        private double deltaLongitude = 3.88484e-05;

        private readonly double ltLatitude = 35.66772396;
        private readonly double ltLongitude = 139.7288072;

        public  List<Telemetry> Temetries { get { return telemetries; } }
        private List<Telemetry> telemetries = new List<Telemetry>();
        public ThingsCar(string deviceId)
        {
            DeviceId = deviceId;
            status = ThingsCarStatus.Parking;
            batteryLevel = maxBL;
        }

        public bool Tracking
        {
            get { return tracking; }
            set { tracking = value; }
        }
        public void GetPosition(double latitude, double longitude, out double px,out double py)
        {
            px = (longitude - ltLongitude) / deltaLongitude;
            py = (latitude - ltLatitude) / deltaLatitude;
        }

        public void UpdatePosition(double px, double py, Point lastPos, DateTime lastPositionUpdate)
        {
            if (batteryLevel < minimumBL)
            {
                Status = ThingsCarStatus.LowBattery;
                return;
            }
            var now = DateTime.Now;
            PosX = px;
            PosY = py;
            var dx = (px - lastPos.X);
            var dy = (py - lastPos.Y);
            var delta = Math.Sqrt(dx * dx + dy * dy);
            BatteryLevel -= delta * deltaBLPerPoint;
            if (lastPositionUpdate != null)
            {
                Status = ThingsCarStatus.Running;
                if (tracking)
                {
                    double speed = delta / (now.Ticks - lastPositionUpdate.Ticks);
                    double direction = 0;
                    if (dx != 0)
                    {

                        direction = Math.Atan(dy / dx) * 180 / Math.PI;
                    }
                    else
                    {
                        if (dy > 0)
                        {
                            direction = 0;
                        }
                        else
                        {
                            direction = 180;
                        }
                    }
                    lock (this)
                    {
                        telemetries.Add(new Telemetry()
                        {
                            MeasuredTime = now,
                            Direction = direction,
                            Speed = speed,
                            Latitude = Latitude,
                            Longitude = Longitude
                        });
                    }
                }
            }
        }

        DispatcherTimer movingTimer;
        DispatcherTimer chargeTimer;
        double chargePlaceLatitude =35.666193;
        double chargePlaceLongitude = 139.758332;
        double radiusInChargePlace = 0.001;

        private bool CheckInChargePlace()
        {
            bool result = false;
            if (chargePlaceLatitude-radiusInChargePlace<=Latitude&&Latitude<=chargePlaceLatitude+radiusInChargePlace
                && chargePlaceLongitude - radiusInChargePlace <= Longitude && Longitude <= chargePlaceLongitude + radiusInChargePlace)
            {
                result = true;
            }
            return result;
        }

        private bool CheckInGoalPlace()
        {
            bool result = false;
            if (!isJustMoving)
            {
                return result;
            }
            if (goalLatitude - radiusInChargePlace <= Latitude && Latitude <= goalLatitude + radiusInChargePlace & goalLongitude - radiusInChargePlace <= Longitude && Longitude <= goalLongitude + radiusInChargePlace)
            {
                result = true;
                isJustMoving = false;
            }
            return result;
        }
        bool isJustMoving = false;
        DateTime chargeTimeToMove;
        public void MoveTo(Point thePlace)
        {
            goalLatitude = ltLatitude + thePlace.Y * deltaLatitude;
            goalLongitude = ltLongitude + thePlace.X * deltaLongitude;
            if (movingTimer!=null&& movingTimer.IsEnabled)
            {
                movingTimer.Stop();
            }
            isJustMoving = true;
            MoveToThePlace();
        }
        public void Charge()
        {
            isJustMoving = false;
            if (CheckInChargePlace())
            {
                StartCharge();
            }
            else
            {
                goalLatitude = chargePlaceLatitude;
                goalLongitude = chargePlaceLongitude;
                MoveToThePlace();
            }
        }
        private void MoveToThePlace()
        {
            movingTimer = new DispatcherTimer();
            movingTimer.Interval = TimeSpan.FromMilliseconds(1000);
            movingTimer.Tick += MovingTimer_Tick;
            chargeTimeToMove = DateTime.Now;
            movingTimer.Start();
            Status = ThingsCarStatus.Running;
        }

        double goalLatitude;
        double goalLongitude;
        private void MovingTimer_Tick(object sender, EventArgs e)
        {
            double goalPosX, goalPosY, delta =5;
            GetPosition(goalLatitude, goalLongitude, out goalPosX,out goalPosY);
            double dx = goalPosX - PosX;
            double dy = goalPosY - PosY;

            double length = Math.Sqrt(dx * dx + dy * dy);

            double nextX = PosX, nextY = PosY;
            nextX += (dx *delta / length);
            nextY += (dy *delta / length);

            UpdatePosition(nextX, nextY, new Point(PosX, PosY),chargeTimeToMove);
            if (CheckInChargePlace())
            {
                movingTimer.Stop();
                StartCharge();
            }
            if (CheckInGoalPlace())
            {
                movingTimer.Stop();
            }
        }

        private void StartCharge()
        {
            chargeTimer = new DispatcherTimer();
            chargeTimer.Interval = TimeSpan.FromMilliseconds(1000);
            chargeTimer.Tick += ChargeTimer_Tick;
            chargeTimer.Start();
            Status = ThingsCarStatus.Charging;
        }

        private void ChargeTimer_Tick(object sender, EventArgs e)
        {
            BatteryLevel += deltaBLPerChargeSec;
            if (batteryLevel>= maxBL)
            {
                chargeTimer.Stop();
                Status = ThingsCarStatus.Parking;
            }
        }

        public string DeviceId { get; private set; }

        public ThingsCarStatus Status
        {
            get { return status; }
            set
            {
                status = value;
                OnPropertyChanged("Status");
            }
        }

        public double PosX
        {
            get
            {
                return posX;
            }
            set
            {
                posX = value;
                Longitude = ltLongitude + posX * deltaLongitude;
                OnPropertyChanged("PosX");
            }
        }

        public double PosY
        {
            get
            {
                return posY;
            }
            set
            {
                posY = value;
                Latitude = ltLatitude + posY * deltaLatitude;
                OnPropertyChanged("PosY");
            }
        }

        public double Latitude
        {
            get { return latitude; }
            set
            {
                latitude = value;
                OnPropertyChanged("Latitude");
            }
        }

        public double Longitude
        {
            get { return longitude; }
            set
            {
                longitude = value;
                OnPropertyChanged("Longitude");
            }
        }

        private readonly double deltaBLPerPoint = 0.005;
        private readonly double deltaBLPerChargeSec = 0.1;
        private readonly double minimumBL = 195;
        private readonly double maxBL = 205;
        public double BatteryLevel
        {
            get { return batteryLevel; }
            set
            {
                batteryLevel = value;
                OnPropertyChanged("BatteryLevel");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    }

    public enum ThingsCarStatus
    {
        Parking,
        Stopping,
        Running,
        LowBattery,
        Charging
    }

    public class Telemetry
    {
        public DateTime MeasuredTime { get; set; }
        public double Speed { get; set; }
        public double Direction { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
