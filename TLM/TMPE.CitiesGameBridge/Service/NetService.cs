namespace CitiesGameBridge.Service {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;

    public class NetService : INetService {
        public static readonly INetService Instance = new NetService();

        private NetService() { }

        // OTHER STUFF --------------------------------------------------------------------------------
    }
}