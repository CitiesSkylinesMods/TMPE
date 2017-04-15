using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenericGameBridge.Service {
	public interface ISimulationService {
		bool LeftHandDrive { get; }
		uint CurrentFrameIndex { get; }
	}
}
