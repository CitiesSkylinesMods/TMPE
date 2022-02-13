namespace TrafficManager.Util.Extensions
{
    using ColossalFramework;

    public static class CitizenUnitExtensions
    {
        private static CitizenUnit[] _citizenUnitBuffer = Singleton<CitizenManager>.instance.m_units.m_buffer;

        internal static ref CitizenUnit ToCitizenUnit(this uint citizenUnit) => ref _citizenUnitBuffer[citizenUnit];
    }
}

