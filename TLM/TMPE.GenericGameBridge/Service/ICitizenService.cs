using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GenericGameBridge.Service {
	public delegate bool CitizenHandler(uint citizenId, ref Citizen citizen);
	public delegate bool CitizenInstanceHandler(ushort citizenInstanceId, ref CitizenInstance citizenInstance);

	public interface ICitizenService {
		bool CheckCitizenFlags(uint citizenId, Citizen.Flags flagMask, Citizen.Flags? expectedResult = default(Citizen.Flags?));
		bool IsCitizenValid(uint citizenId);
		void ProcessCitizen(uint citizenId, CitizenHandler handler);
		void ProcessCitizen(uint citizenId, ref Citizen citizen, CitizenHandler handler);

		bool CheckCitizenInstanceFlags(ushort citizenInstanceId, CitizenInstance.Flags flagMask, CitizenInstance.Flags? expectedResult = default(CitizenInstance.Flags?));
		bool IsCitizenInstanceValid(ushort citizenInstanceId);
		void ProcessCitizenInstance(ushort citizenInstanceId, CitizenInstanceHandler handler);
		void ProcessCitizenInstance(ushort citizenInstanceId, ref CitizenInstance citizenInstance, CitizenInstanceHandler handler);
	}
}
