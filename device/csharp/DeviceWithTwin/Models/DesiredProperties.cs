using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceWithTwin.Models
{
    public class DesiredProperties : INotifyPropertyChanged
    {
        private string deviceType;
        private int telemetryCycle;
        private double latitude;
        private double longitude;

        public string DeviceType
        {
            get { return deviceType; }
            set
            {
                deviceType = value;
                OnPropertyChanged("DeviceType");
            }
        }

        public int TelemetryCycle
        {
            get { return telemetryCycle; }
            set
            {
                telemetryCycle = value;
                OnPropertyChanged("TelemetryCycle");
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

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }

        public void Deserialize(string json)
        {
            Newtonsoft.Json.Linq.JObject jobject = Newtonsoft.Json.JsonConvert.DeserializeObject(json) as Newtonsoft.Json.Linq.JObject;
            var deviceTypeToken = (Newtonsoft.Json.Linq.JValue)jobject.SelectToken("dmConfig.DeviceType");
            var telemetryCycleToken = (Newtonsoft.Json.Linq.JValue)jobject.SelectToken("dmConfig.TelemetryCycle");
            var latitudeToken = (Newtonsoft.Json.Linq.JValue)jobject.SelectToken("dmConfig.Latitude");
            var longitudeToken = (Newtonsoft.Json.Linq.JValue)jobject.SelectToken("dmConfig.Longitude");
            DeviceType = deviceTypeToken.Value.ToString();
            TelemetryCycle = int.Parse(telemetryCycleToken.Value.ToString());
            Latitude = (double)latitudeToken.Value;
            Longitude = (double)longitudeToken.Value;

        }
    }
}
