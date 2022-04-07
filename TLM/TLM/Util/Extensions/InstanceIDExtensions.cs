namespace TrafficManager.Util.Extensions {
    using System.Text;
    using UnityEngine;

    public static class InstanceIDExtensions {

        public static Vector3 GetWorldPos(ref this InstanceID id, bool fast) => id.Type switch {
            InstanceType.Building => id.Building.ToBuilding().m_position,
            InstanceType.Vehicle => fast
                ? id.Vehicle.ToVehicle().GetLastFramePosition()
                : id.Vehicle.ToVehicle().GetSmoothPosition(id.Vehicle),
//            InstanceType.District => ?,
//            InstanceType.Citizen => ?,
            InstanceType.NetNode => id.NetNode.ToNode().m_position,
            InstanceType.NetSegment => fast
                ? id.NetSegment.ToSegment().m_middlePosition
                : id.NetSegment.ToSegment().m_bounds.center,
            InstanceType.ParkedVehicle => id.ParkedVehicle.ToParkedVehicle().m_position,
//            InstanceType.TransportLine => ,
            InstanceType.CitizenInstance => fast
                ? id.CitizenInstance.ToCitizenInstance().GetLastFramePosition()
                : id.CitizenInstance.ToCitizenInstance().GetSmoothPosition(id.CitizenInstance),
//            InstanceType.Prop => ?,
//            InstanceType.Tree => ?,
//            InstanceType.Event => ?,
//            InstanceType.NetLane => ,
//            InstanceType.BuildingProp => ,
//            InstanceType.NetLaneProp => ,
//            InstanceType.Disaster => ,
//            InstanceType.Lightning => ,
//            InstanceType.RadioChannel => ,
//            InstanceType.RadioContent => ,
//            InstanceType.Park => ,
            _ => Vector3.zero, // TODO: point should be screen center in world space
        };

        public static string GetTag(ref this InstanceID id) => id.Type switch {
            InstanceType.Building => new StringBuilder("B:", 7).Append(id.Building).ToString(),
            InstanceType.Vehicle => new StringBuilder("V:", 7).Append(id.Vehicle).ToString(),
            InstanceType.District => new StringBuilder("D:", 5).Append(id.District).ToString(),
            InstanceType.Citizen => new StringBuilder("C:", 12).Append(id.Citizen).ToString(),
            InstanceType.NetNode => new StringBuilder("N:", 7).Append(id.NetNode).ToString(),
            InstanceType.NetSegment => new StringBuilder("S:", 7).Append(id.NetSegment).ToString(),
            InstanceType.ParkedVehicle => new StringBuilder("PV:", 7).Append(id.ParkedVehicle).ToString(),
            InstanceType.TransportLine => new StringBuilder("TL:", 7).Append(id.TransportLine).ToString(),
            InstanceType.CitizenInstance => new StringBuilder("CI:", 7).Append(id.CitizenInstance).ToString(),
            InstanceType.Prop => new StringBuilder("P:", 7).Append(id.Prop).ToString(),
            InstanceType.Tree => new StringBuilder("T:", 12).Append(id.Tree).ToString(),
            InstanceType.Event => new StringBuilder("E:", 7).Append(id.Event).ToString(),
            InstanceType.NetLane => new StringBuilder("L:", 12).Append(id.NetLane).ToString(),
            InstanceType.BuildingProp => new StringBuilder("BP:", 5).Append(GetPropIndex(ref id)).ToString(), // max 64 prop per bldg?
            InstanceType.NetLaneProp => new StringBuilder("LP:", 5).Append(GetPropIndex(ref id)).ToString(), // max 64 prop per lane?
            InstanceType.Disaster => new StringBuilder("ND:", 7).Append(id.Disaster).ToString(),
            InstanceType.Lightning => new StringBuilder("Zap:", 7).Append(id.Lightning).ToString(),
            InstanceType.RadioChannel => new StringBuilder("Ch:", 7).Append(id.RadioChannel).ToString(),
            InstanceType.RadioContent => new StringBuilder("RC:", 7).Append(id.RadioContent).ToString(),
            InstanceType.Park => new StringBuilder("PA:", 6).Append(id.Park).ToString(),
            _ => new StringBuilder("!:", 17).Append(id.RawData).ToString(),
        };

        private static int GetPropIndex(ref InstanceID id) {
            switch (id.Type) {
                case InstanceType.BuildingProp:
                    id.GetBuildingProp(out var _, out int buildingPropIndex);
                    return buildingPropIndex;
                case InstanceType.NetLaneProp:
                    id.GetNetLaneProp(out var _, out int lanePropIndex);
                    return lanePropIndex;
                default:
                    return 0;
            }
        }
    }
}
