using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Geometry;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using TrafficManager.Traffic.Data;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;
using CSUtil.Commons.Benchmark;
using static TrafficManager.Custom.PathFinding.CustomPathManager;
using TrafficManager.Traffic.Enums;
using TrafficManager.RedirectionFramework.Attributes;
using System.Runtime.CompilerServices;

namespace TrafficManager.Custom.AI {
	[TargetType(typeof(CitizenAI))]
	public class CustomCitizenAI : CitizenAI {
		[RedirectMethod]
		public bool CustomStartPathFind(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo, bool enableTransport, bool ignoreCost) {
			IExtCitizenInstanceManager extCitizenInstanceManager = Constants.ManagerFactory.ExtCitizenInstanceManager;
			IExtCitizenManager extCitizenManager = Constants.ManagerFactory.ExtCitizenManager;
			return extCitizenInstanceManager.StartPathFind(instanceID, ref citizenData, ref extCitizenInstanceManager.ExtInstances[instanceID], ref extCitizenManager.ExtCitizens[citizenData.m_citizen], startPos, endPos, vehicleInfo, enableTransport, ignoreCost);
		}
	}
}
