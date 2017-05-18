using ColossalFramework;
using CSUtil.Commons;
using GenericGameBridge.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CitiesGameBridge.Service {
	public class CitizenService : ICitizenService {
		public static readonly ICitizenService Instance = new CitizenService();

		private CitizenService() {

		}

		public bool CheckCitizenFlags(uint citizenId, Citizen.Flags flagMask, Citizen.Flags? expectedResult = default(Citizen.Flags?)) {
			bool ret = false;
			ProcessCitizen(citizenId, delegate (uint cId, ref Citizen citizen) {
				ret = LogicUtil.CheckFlags((uint)citizen.m_flags, (uint)flagMask, (uint?)expectedResult);
				return true;
			});
			return ret;
		}

		public bool CheckCitizenInstanceFlags(ushort citizenInstanceId, CitizenInstance.Flags flagMask, CitizenInstance.Flags? expectedResult = default(CitizenInstance.Flags?)) {
			bool ret = false;
			ProcessCitizenInstance(citizenInstanceId, delegate (ushort ciId, ref CitizenInstance citizenInstance) {
				ret = LogicUtil.CheckFlags((uint)citizenInstance.m_flags, (uint)flagMask, (uint?)expectedResult);
				return true;
			});
			return ret;
		}

		public bool IsCitizenInstanceValid(ushort citizenInstanceId) {
			return CheckCitizenInstanceFlags(citizenInstanceId, CitizenInstance.Flags.Created | CitizenInstance.Flags.Deleted, CitizenInstance.Flags.Created);
		}

		public bool IsCitizenValid(uint citizenId) {
			return CheckCitizenFlags(citizenId, Citizen.Flags.Created);
		}

		public void ProcessCitizen(uint citizenId, CitizenHandler handler) {
			ProcessCitizen(citizenId, ref Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId], handler);
		}

		public void ProcessCitizen(uint citizenId, ref Citizen citizen, CitizenHandler handler) {
			handler(citizenId, ref citizen);
		}

		public void ProcessCitizenInstance(ushort citizenInstanceId, CitizenInstanceHandler handler) {
			ProcessCitizenInstance(citizenInstanceId, ref Singleton<CitizenManager>.instance.m_instances.m_buffer[citizenInstanceId], handler);
		}

		public void ProcessCitizenInstance(ushort citizenInstanceId, ref CitizenInstance citizenInstance, CitizenInstanceHandler handler) {
			handler(citizenInstanceId, ref citizenInstance);
		}
	}
}
