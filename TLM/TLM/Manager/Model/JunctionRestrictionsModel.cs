using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.Manager.Model {
    internal struct JunctionRestrictionsModel {

        public JunctionRestrictionsFlags values;

        public JunctionRestrictionsFlags mask;
    }
}
