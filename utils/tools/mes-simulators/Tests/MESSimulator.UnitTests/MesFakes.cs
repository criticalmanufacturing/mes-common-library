using System.Reflection;
using MESSimulator.Mes;

namespace MESSimulator.UnitTests
{
    /// <summary>
    /// A fake of one MES gateway interface: every call is recorded as "{Interface}.{Method}"; a handler by method name
    /// answers it, otherwise it returns nothing (void), the default of a value type, an empty list, or null.
    /// </summary>
    public class MesProxy<T> : DispatchProxy where T : class
    {
        public Dictionary<string, Func<object?[], object?>> Handlers { get; } = [];

        public List<string> Calls { get; set; } = [];

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            lock (Calls)
            {
                Calls.Add($"{typeof(T).Name[1..]}.{method!.Name}");
            }
            if (Handlers.TryGetValue(method.Name, out var handler))
            {
                return handler(args ?? []);
            }

            var type = method.ReturnType;
            if (type == typeof(void)) return null;
            if (type.IsValueType) return Activator.CreateInstance(type);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return Activator.CreateInstance(type);
            return null;
        }

        public static (T Gateway, MesProxy<T> Fake) Create(List<string> calls)
        {
            var gateway = Create<T, MesProxy<T>>();
            var fake = (MesProxy<T>)(object)gateway;
            fake.Calls = calls;
            return (gateway, fake);
        }
    }

    /// <summary>An <see cref="IMesGateway"/> made of <see cref="MesProxy{T}"/> fakes sharing one call list.</summary>
    public sealed class FakeMes : IMesGateway
    {
        public FakeMes(List<string>? calls = null)
        {
            Calls = calls ?? [];
            (Materials, MaterialsFake) = MesProxy<IMaterialGateway>.Create(Calls);
            (Resources, ResourcesFake) = MesProxy<IResourceGateway>.Create(Calls);
            (Batches, BatchesFake) = MesProxy<IBatchGateway>.Create(Calls);
            (Setup, SetupFake) = MesProxy<ISetupGateway>.Create(Calls);
            (Labor, LaborFake) = MesProxy<ILaborGateway>.Create(Calls);
            (MasterData, MasterDataFake) = MesProxy<IMasterDataGateway>.Create(Calls);
        }

        public List<string> Calls { get; }

        public IMaterialGateway Materials { get; }
        public IResourceGateway Resources { get; }
        public IBatchGateway Batches { get; }
        public ISetupGateway Setup { get; }
        public ILaborGateway Labor { get; }
        public IMasterDataGateway MasterData { get; }

        public MesProxy<IMaterialGateway> MaterialsFake { get; }
        public MesProxy<IResourceGateway> ResourcesFake { get; }
        public MesProxy<IBatchGateway> BatchesFake { get; }
        public MesProxy<ISetupGateway> SetupFake { get; }
        public MesProxy<ILaborGateway> LaborFake { get; }
        public MesProxy<IMasterDataGateway> MasterDataFake { get; }
    }
}
