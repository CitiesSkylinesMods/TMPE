using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public interface ICustomDataManager<T> {
		// TODO documentation
		bool LoadData(T data);
		T SaveData(ref bool success);
	}
}
