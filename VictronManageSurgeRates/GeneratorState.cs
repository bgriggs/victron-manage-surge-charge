namespace VictronManageSurgeRates;

public enum GeneratorState
{
    Stopped,
    Running,
    WarmUp,
    CoolDown,
    Stopping,
    Error = 10
}
