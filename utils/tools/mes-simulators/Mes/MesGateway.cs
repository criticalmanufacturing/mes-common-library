namespace MESSimulator.Mes
{
    /// <summary>
    /// All MES access, grouped by area. Every call goes through <see cref="IMesCall"/> (retry + timing).
    /// </summary>
    public interface IMesGateway
    {
        IMaterialGateway Materials { get; }
        IResourceGateway Resources { get; }
        IBatchGateway Batches { get; }
        ISetupGateway Setup { get; }
        ILaborGateway Labor { get; }
        IMasterDataGateway MasterData { get; }
    }

    public sealed class MesGateway(
        IMaterialGateway materials,
        IResourceGateway resources,
        IBatchGateway batches,
        ISetupGateway setup,
        ILaborGateway labor,
        IMasterDataGateway masterData) : IMesGateway
    {
        public IMaterialGateway Materials { get; } = materials;
        public IResourceGateway Resources { get; } = resources;
        public IBatchGateway Batches { get; } = batches;
        public ISetupGateway Setup { get; } = setup;
        public ILaborGateway Labor { get; } = labor;
        public IMasterDataGateway MasterData { get; } = masterData;
    }
}
