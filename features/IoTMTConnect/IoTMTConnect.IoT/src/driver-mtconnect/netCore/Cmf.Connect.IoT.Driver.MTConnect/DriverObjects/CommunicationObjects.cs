namespace Cmf.Connect.IoT.Driver.MTConnect.DriverObjects
{
    /// <summary>
    /// Type of execute command 
    /// </summary>
    public enum CommandType
    {
        Probe = 0,
        Current = 1,
        Sample = 2,
        Assets = 3,
        Asset = 4
    }

    /// <summary>
    /// Type of event 
    /// </summary>
    public enum EventType
    {
        Probe = 0,
        Current = 1,
        Sample = 2,
        Assets = 3
    }
}
