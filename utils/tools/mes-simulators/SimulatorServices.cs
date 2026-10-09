using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MESSimulator.Line;
using MESSimulator.Mes;
using MESSimulator.Pipeline;
using MESSimulator.Steps;
using MESSimulator.Steps.Actions;

namespace MESSimulator
{
    /// <summary>
    /// Simulation settings ("Simulation" configuration section; --speed overrides Speed).
    /// </summary>
    public sealed class SimulationOptions
    {
        public const string SectionName = "Simulation";

        /// <summary>Divides every simulated delay (process times, launch and batch check intervals). Must be &gt; 0.</summary>
        public decimal Speed { get; set; } = 100m;

        /// <summary>
        /// Shortest real time between two retries of something only the MES can tell (a free slot, a busy feeder, a batch
        /// queue): at a high speed the scaled waits shrink to milliseconds and would flood the MES.
        /// </summary>
        public TimeSpan MinPollInterval { get; set; } = TimeSpan.FromSeconds(5);
    }

    public static class SimulatorServices
    {
        /// <summary>
        /// Registers the MES client and gateways (retry + timing), the line from simulationConfiguration.json, the step engine and the
        /// pipeline. Shared by the console host and the integration tests (which do not add the hosted service).
        /// </summary>
        public static IServiceCollection AddMESSimulator(this IServiceCollection services, IConfiguration configuration)
        {
            Program.ConfigureClient(configuration);
            // The host registers its own; without a host (e.g. the wiring tests) this one is used
            services.TryAddSingleton(configuration);

            services.AddOptions<SimulationOptions>()
                .Bind(configuration.GetSection(SimulationOptions.SectionName))
                .Validate(o => o.Speed > 0, "Simulation:Speed must be > 0.")
                .Validate(o => o.MinPollInterval >= TimeSpan.Zero, "Simulation:MinPollInterval must be >= 0.")
                .ValidateOnStart();
            services.AddOptions<MesOptions>()
                .Bind(configuration.GetSection(MesOptions.SectionName))
                .Validate(o => !o.Errors().Any(), "Invalid Mes section: RetryMaxAttempts 0-10, RetryDelay 0-30 s, MaxConcurrentCalls >= 1.")
                .ValidateOnStart();
            services.Configure<DiagnosticsOptions>(configuration.GetSection(DiagnosticsOptions.SectionName));
            services.Configure<ResourceOccupancyOptions>(configuration.GetSection(ResourceOccupancyOptions.SectionName));
            services.AddOptions<LineOptions>()
                .Bind(configuration.GetSection(LineOptions.SectionName))
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<LineOptions>, LineOptionsValidator>();

            // MES access
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<MesCallStatistics>();
            services.AddSingleton<IMesCall, MesCall>();
            services.AddSingleton<IMaterialGateway, MaterialGateway>();
            services.AddSingleton<IResourceGateway, ResourceGateway>();
            services.AddSingleton<IBatchGateway, BatchGateway>();
            services.AddSingleton<ISetupGateway, SetupGateway>();
            services.AddSingleton<ILaborGateway, LaborGateway>();
            services.AddSingleton<IMasterDataGateway, MasterDataGateway>();
            services.AddSingleton<IMesGateway, MesGateway>();

            // Step engine: line, resources, locks, tracking, the safety valve, actions and conditions
            services.AddSingleton<LineDefinition>();
            // Line:Chaos:Seed makes a run repeatable (resources, times, rework, scrap)
            services.AddSingleton<IRandomSource>(sp =>
                sp.GetRequiredService<IOptions<LineOptions>>().Value.Chaos.Seed is int seed
                    ? new RandomSource(new Random(seed))
                    : new RandomSource(Random.Shared));
            services.AddSingleton<SimulationClock>();
            services.AddSingleton<IStepResourceSource, MesStepResourceSource>();
            services.AddSingleton<ResourceCatalog>();
            services.AddSingleton<ResourceLocks>();
            services.AddSingleton<StateModelCache>();
            services.AddSingleton<IMaterialTracker, MaterialTracker>();
            services.AddSingleton<IResourceOccupancyPolicy, ResourceOccupancyPolicy>();
            services.AddSingleton<IOperatorCheckIn, OperatorCheckIn>();
            services.AddSingleton<IResourceEligibility, ResourceServiceEligibility>();
            services.AddSingleton<SetUpLots>();
            services.AddSingleton<StepExecutor>();

            services.AddStepAction<FeedersAction>(FeedersAction.ActionKey, FeedersAction.ActionHook);
            services.AddStepAction<DurablesAction>(DurablesAction.ActionKey, DurablesAction.ActionHook);
            services.AddStepAction<ComposeAction>(ComposeAction.ActionKey, ComposeAction.ActionHook);
            services.AddStepAction<SubMaterialTrackingAction>(SubMaterialTrackingAction.ActionKey, SubMaterialTrackingAction.ActionHook);
            services.AddStepAction<SplitTrackOutAction>(SplitTrackOutAction.ActionKey, SplitTrackOutAction.ActionHook);
            services.AddStepAction<LineFlowAction>(LineFlowAction.ActionKey, LineFlowAction.ActionHook);
            services.AddStepAction<ShipAction>(ShipAction.ActionKey, ShipAction.ActionHook);
            services.AddSingleton<IShipper, ShipAction>();
            services.AddSingleton<IProductionOrderCompletion, ProductionOrderCompletion>();
            services.AddSingleton<IReworkChaos, ReworkChaos>();
            services.AddStepAction<PackAction>(PackAction.ActionKey, PackAction.ActionHook);
            services.AddStepAction<AssembleAction>(AssembleAction.ActionKey, AssembleAction.ActionHook);
            services.AddStepAction<LotPackAction>(LotPackAction.ActionKey, LotPackAction.ActionHook);
            services.AddStepAction<PalletPackAction>(PalletPackAction.ActionKey, PalletPackAction.ActionHook);
            services.AddStepAction<DataCollectionAction>(DataCollectionAction.ActionKey, DataCollectionAction.ActionHook);
            services.AddStepAction<LotSplitTrackOutAction>(LotSplitTrackOutAction.ActionKey, LotSplitTrackOutAction.ActionHook);
            services.AddStepCondition<InspCdRequiredCondition>(InspCdRequiredCondition.ConditionKey);

            // Pipeline: orders -> lot flows <-> batch steps
            services.AddSingleton<InFlightLots>();
            services.AddSingleton<BatchQueues>();
            services.AddSingleton<LotFlow>();
            services.AddSingleton<BatchProcessor>();
            services.AddSingleton<OrderSource>();
            services.AddSingleton<PreviousRunCleanup>();
            services.AddSingleton<SharedFeeders>();
            services.AddSingleton<IFailedLotHandler, FailedLotHandler>();
            services.AddSingleton<ITerminationConfirmation, ConsoleTerminationConfirmation>();
            services.AddSingleton<RunOutcome>();
            services.AddSingleton<LineStartup>();

            return services;
        }

        /// <summary>Registers a step action and its descriptor (used to validate simulationConfiguration.json without building the action).</summary>
        public static IServiceCollection AddStepAction<TAction>(this IServiceCollection services, string key, StepHook hook)
            where TAction : class, IStepAction
        {
            services.AddSingleton<IStepAction, TAction>();
            services.AddSingleton(new StepActionDescriptor(key, hook));
            return services;
        }

        /// <summary>Registers a step condition and its descriptor.</summary>
        public static IServiceCollection AddStepCondition<TCondition>(this IServiceCollection services, string key)
            where TCondition : class, IStepCondition
        {
            services.AddSingleton<IStepCondition, TCondition>();
            services.AddSingleton(new StepConditionDescriptor(key));
            return services;
        }
    }
}
