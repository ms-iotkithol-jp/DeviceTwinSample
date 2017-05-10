using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceWithTwin.Models
{
    public class ReportedProperties
    {
        public double RealLatitude { get; set; }
        public double RealLongitude { get; set; }
        public double BatteryLevel { get; set; }
        public Models.ThingsCarStatus Status { get; set; }
    }
}
