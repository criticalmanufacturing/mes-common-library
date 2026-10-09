using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// Steps with a data collection the lot must fill before it tracks out (e.g. paint thickness at PLATE COLORING):
    /// after the track-in, posts one reading per parameter of the lot's current data collection, its nominal value
    /// (Line:Steps:{step}:DataCollection:Values) ±Variation, read with the configured instrument.
    /// </summary>
    public sealed class DataCollectionAction(IMesGateway mes, IMaterialTracker tracker, IRandomSource random, ILogger<DataCollectionAction> logger) : IStepAction
    {
        public const string ActionKey = "dataCollection";
        public const StepHook ActionHook = StepHook.AfterTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        /// <summary>The nominal value moved by a random fraction in [-variation, +variation], rounded to 2 decimals.</summary>
        public static decimal Reading(decimal nominal, decimal variation, int permille) =>
            Math.Round(nominal * (1 + variation * (permille - 1000) / 1000m), 2, MidpointRounding.AwayFromZero);

        public Task ExecuteAsync(StepContext context)
        {
            var options = context.Step.DataCollection
                ?? throw new InvalidOperationException($"Step '{context.Step.Name}' runs the dataCollection action but has no DataCollection values in simulationConfiguration.json");

            var lot = mes.Materials.LoadBasicInformation(tracker.Reload(context.Lot));
            var instance = lot.CurrentDataCollectionInstance;
            if (instance == null)
            {
                logger.LogDebug($"'{lot.Name}' has no data collection at {context.Step.Name}");
                return Task.CompletedTask;
            }

            var dataCollection = mes.Materials.LoadDataCollectionParameters(lot);
            var instrument = options.Instrument == null ? null
                : mes.Resources.GetByName(options.Instrument)
                    ?? throw new InvalidOperationException($"Instrument '{options.Instrument}' (Line:Steps:{context.Step.Name}:DataCollection:Instrument) not found");

            var points = new DataCollectionPointCollection();
            foreach (var parameter in dataCollection.DataCollectionParameters.Select(p => p.TargetEntity))
            {
                if (!options.Values.TryGetValue(parameter.Name, out var nominal))
                {
                    throw new InvalidOperationException($"No value for data collection parameter '{parameter.Name}' (Line:Steps:{context.Step.Name}:DataCollection:Values)");
                }

                points.Add(new DataCollectionPoint()
                {
                    Instrument = instrument?.Id.ToString(),
                    InstrumentName = instrument?.Name,
                    ReadingNumber = 1,
                    SampleId = "Sample 1",
                    SourceEntity = instance,
                    TargetEntity = parameter,
                    Value = Reading(nominal, options.Variation, random.Next(0, 2001))
                });
            }

            context.Lot = mes.Materials.PostDataCollectionPoints(instance, points);
            logger.LogInformation($"Posted {points.Count} reading(s) of '{dataCollection.Name}' for '{lot.Name}' at {context.Step.Name}");
            return Task.CompletedTask;
        }
    }
}
