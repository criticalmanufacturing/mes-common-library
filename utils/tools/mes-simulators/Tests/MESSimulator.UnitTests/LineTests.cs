using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MESSimulator.Line;
using MESSimulator.Pipeline;
using MESSimulator.Steps;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    public class BatchPlannerTests
    {
        private static Material LotOf(string name, int wafers, int minutesAgo) => new()
        {
            Name = name,
            PrimaryQuantity = wafers,
            DateEnteredStep = DateTime.UtcNow.AddMinutes(-minutesAgo)
        };

        [Fact]
        public void Plan_WaitsWhileTheLotsAreUnderTheMinimum()
        {
            var plan = BatchPlanner.Plan([LotOf("A", 25, 3), LotOf("B", 20, 2)], minQuantity: 70, maxQuantity: 100);

            Assert.False(plan.HasBatch);
            Assert.Equal(["A", "B"], plan.Leftovers.Select(l => l.Name));
        }

        [Fact]
        public void Plan_RunsOneBatchOnceTheMinimumIsReached()
        {
            var plan = BatchPlanner.Plan([LotOf("A", 25, 3), LotOf("B", 25, 2), LotOf("C", 20, 1)], 70, 100);

            Assert.True(plan.HasBatch);
            Assert.Equal(["A", "B", "C"], plan.BatchLots.Select(l => l.Name));
            Assert.Equal(70, plan.Quantity);
            Assert.Empty(plan.Leftovers);
        }

        [Fact]
        public void Plan_OverTheMaximum_GivesACompleteBatchAndLeavesTheRestWaiting()
        {
            var plan = BatchPlanner.Plan([LotOf("A", 25, 5), LotOf("B", 25, 4), LotOf("C", 25, 3), LotOf("D", 25, 2), LotOf("E", 10, 1)], 70, 100);

            Assert.Equal(["A", "B", "C", "D"], plan.BatchLots.Select(l => l.Name));
            Assert.Equal(100, plan.Quantity);
            Assert.Equal(["E"], plan.Leftovers.Select(l => l.Name));
        }

        [Fact]
        public void Plan_KeepsArrivalOrder_ASmallerLaterLotDoesNotJumpAhead()
        {
            // B does not fit (60 + 50 > 100); C would fit but must not overtake B
            var plan = BatchPlanner.Plan([LotOf("A", 60, 3), LotOf("B", 50, 2), LotOf("C", 20, 1)], 50, 100);

            Assert.Equal(["A"], plan.BatchLots.Select(l => l.Name));
            Assert.Equal(["B", "C"], plan.Leftovers.Select(l => l.Name));
        }

        [Fact]
        public void Plan_OrdersByTimeEnteredStep_NotByQueueOrder()
        {
            var plan = BatchPlanner.Plan([LotOf("Newest", 40, 1), LotOf("Oldest", 40, 9)], 70, 100);

            Assert.Equal(["Oldest", "Newest"], plan.BatchLots.Select(l => l.Name));
        }

        [Fact]
        public void Plan_RunsAnOversizedLotOnItsOwn()
        {
            var plan = BatchPlanner.Plan([LotOf("Big", 150, 2), LotOf("Small", 10, 1)], 70, 100);

            Assert.Equal(["Big"], plan.BatchLots.Select(l => l.Name));
            Assert.Equal(["Small"], plan.Leftovers.Select(l => l.Name));
        }

        [Fact]
        public void LotQuantity_UsesTheSubMaterialsWhenTheLotHasNoQuantity()
        {
            Assert.Equal(12, BatchPlanner.LotQuantity(new Material { PrimaryQuantity = 0, SubMaterialsPrimaryQuantity = 12 }));
            Assert.Equal(7, BatchPlanner.LotQuantity(new Material { PrimaryQuantity = 7, SubMaterialsPrimaryQuantity = 12 }));
        }
    }

    public class LineDefinitionTests
    {
        [Theory]
        [InlineData("Y31_CSP_3L:A:1/PHOTO FUSE:A:3/ASHING PRE:1", "ASHING PRE")]
        [InlineData("Y31_CSP_3L:A:1/WAFER PREP:A:1/SILICON WAFER COMPOSE:2", "SILICON WAFER COMPOSE")]
        [InlineData("Kanban WPREP:A:1/Kanban WPREP:1", "Kanban WPREP")]
        [InlineData("PRE_CURE", "PRE_CURE")]
        public void CurrentStepName_IsTheLastSegmentOfTheFlowPath(string flowPath, string expected)
        {
            Assert.Equal(expected, LineDefinition.CurrentStepName(new Material { FlowPath = flowPath }));
        }

        [Fact]
        public void FindStep_IgnoresCase()
        {
            var line = TestLine();

            Assert.NotNull(line.FindStep("EXPOSE"));
            Assert.True(line.IsBatchStep("pre_cure"));
            Assert.Null(line.FindStep("SAR CAPTURE RESULT"));
        }

        [Fact]
        public void LineJson_BindsAndIsValid()
        {
            // The shipped simulationConfiguration.semi.json, copied next to the tests
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("simulationConfiguration.semi.json", optional: false)
                .Build();
            var options = configuration.GetSection(LineOptions.SectionName).Get<LineOptions>()!;

            var result = Validator().Validate(null, options);

            Assert.True(result.Succeeded, result.FailureMessage);
            var line = new LineDefinition(Options.Create(options));
            Assert.True(line.IsBatchStep("PRE_CURE"));
            Assert.True(line.IsBatchStep("CureWafers"));
            Assert.Equal(["splitTrackOut"], line.FindStep("ASHING PRE")!.Actions);
            Assert.True(line.FindStep("Expose")!.SingleMaterial);
            Assert.Equal(0.1m, line.Consumables.For("Al Target").LowQuantity);
            Assert.Equal(2000m, line.Consumables.For("BG Tape").LowQuantity);
            Assert.Contains("Dee_Notification_ExitStep", line.Startup.DisableDeeActions);
            Assert.Equal("KommSemi Calendar", line.Startup.Operator.Calendar);
        }

        private static LineOptionsValidator Validator() =>
            new(
                [
                    new StepActionDescriptor("feeders", StepHook.BeforeTrackIn),
                    new StepActionDescriptor("durables", StepHook.BeforeTrackIn),
                    new StepActionDescriptor("compose", StepHook.AfterTrackIn),
                    new StepActionDescriptor("subMaterialTracking", StepHook.AfterTrackIn),
                    new StepActionDescriptor("splitTrackOut", StepHook.TrackOut),
                    new StepActionDescriptor("lineFlow", StepHook.AfterTrackIn),
                    new StepActionDescriptor("ship", StepHook.TrackOut),
                    new StepActionDescriptor("pack", StepHook.AfterTrackIn),
                    new StepActionDescriptor("split", StepHook.TrackOut)
                ],
                [new StepConditionDescriptor("inspCdRequired")]);

        [Fact]
        public void Validator_AcceptsTheTestLine()
        {
            Assert.True(Validator().Validate(null, TestLineOptions()).Succeeded);
        }

        [Fact]
        public void Validator_RejectsAnUnknownAction()
        {
            var options = TestLineOptions();
            options.Steps["COAT"].Actions = ["feedrs"];

            var result = Validator().Validate(null, options);

            Assert.True(result.Failed);
            Assert.Contains("Line:Steps:COAT: unknown action 'feedrs'", result.FailureMessage);
        }

        [Fact]
        public void Validator_RejectsAnUnknownCondition()
        {
            var result = Validator().Validate(null, TestLineOptions(inspCdCondition: "inspCdRequiredd"));

            Assert.Contains("unknown condition 'inspCdRequiredd'", result.FailureMessage);
        }

        [Fact]
        public void Validator_RejectsInvalidRanges()
        {
            var options = TestLineOptions();
            options.Steps["COAT"].MinSeconds = 60;
            options.Steps["COAT"].MaxSeconds = 20;
            options.Steps["PRE_CURE"].Batch!.MinQuantity = 120;

            var result = Validator().Validate(null, options);

            Assert.Contains("Line:Steps:COAT: time range [60, 20] is invalid", result.FailureMessage);
            Assert.Contains("Line:Steps:PRE_CURE: batch quantity range [120, 100] is invalid", result.FailureMessage);
        }

        [Fact]
        public void Validator_RejectsTwoTrackOutActionsOnOneStep()
        {
            var options = TestLineOptions();
            options.Steps["ASHING PRE"].Actions = ["split", "splitTrackOut"];

            Assert.Contains("more than one track-out action", Validator().Validate(null, options).FailureMessage);
        }

        [Fact]
        public void Validator_RejectsResourcesOnAPassThroughStep()
        {
            var options = TestLineOptions();
            options.Steps["Wafer Shipping FE"].Resources = ["PACK_WS-001"];

            Assert.Contains("Line:Steps:Wafer Shipping FE: a pass-through step cannot have resources", Validator().Validate(null, options).FailureMessage);
        }

        [Fact]
        public void Validator_RejectsShipOnAStepThatIsNotPassThrough()
        {
            var options = TestLineOptions();
            options.Steps["COAT"].Ship = true;

            Assert.Contains("Line:Steps:COAT: Ship is for pass-through steps", Validator().Validate(null, options).FailureMessage);
        }

        [Fact]
        public void Validator_RejectsNotReceivingOnAStepThatDoesNotShip()
        {
            var options = TestLineOptions();
            options.Steps["Wafer Reception BE"].Receive = false;

            Assert.Contains("Line:Steps:Wafer Reception BE: Receive=false only applies to a step that ships", Validator().Validate(null, options).FailureMessage);
        }

        [Fact]
        public void Validator_RejectsInvalidChaos()
        {
            var options = TestLineOptions();
            options.Chaos.ReworkFlows["RWK_FUSE Photoresist"] = 1.5m;
            options.Chaos.ScrapProbability = 0.2m;
            options.Chaos.MaxScrapFraction = 0;

            var message = Validator().Validate(null, options).FailureMessage;

            Assert.Contains("Line:Chaos:ReworkFlows:RWK_FUSE Photoresist must be between 0 and 1", message);
            Assert.Contains("Line:Chaos:MaxScrapFraction must be > 0 and <= 1", message);
            Assert.Contains("Line:Chaos:ScrapReasons is required when ScrapProbability > 0", message);
        }

        [Fact]
        public void Validator_RejectsActionsOnABatchStep()
        {
            var options = TestLineOptions();
            options.Steps["PRE_CURE"].Actions = ["feeders"];

            Assert.Contains("a batch step cannot have actions", Validator().Validate(null, options).FailureMessage);
        }

        [Fact]
        public void ConsumableRules_ProductOverridesOnlyWhatItSets()
        {
            var consumables = new ConsumablesOptions
            {
                Default = new ConsumableRules { LowQuantity = 2000, ReplacementQuantity = 60000, TerminateReason = "Full Loss" },
                Products = new(StringComparer.OrdinalIgnoreCase) { ["Ag Target"] = new ConsumableRules { LowQuantity = 0.1m, ReplacementQuantity = 1 } }
            };

            var target = consumables.For("ag target");
            var tape = consumables.For("BG Tape");

            Assert.Equal((0.1m, 1m, "Full Loss"), (target.LowQuantity, target.ReplacementQuantity, target.TerminateReason));
            Assert.Equal((2000m, 60000m, "Full Loss"), (tape.LowQuantity, tape.ReplacementQuantity, tape.TerminateReason));
        }

        [Fact]
        public void Validator_RejectsAReplacementQuantityNotAboveTheLowQuantity()
        {
            var options = TestLineOptions();
            options.Consumables.Products["Ag Target"] = new ConsumableRules { LowQuantity = 1, ReplacementQuantity = 1 };

            Assert.Contains("Line:Consumables:Products:Ag Target: ReplacementQuantity must be > LowQuantity",
                Validator().Validate(null, options).FailureMessage);
        }

        [Fact]
        public void Validator_RequiresTheOperatorCalendarWhenCheckingIn()
        {
            var options = TestLineOptions();
            options.Startup.Operator.Calendar = null;

            Assert.Contains("Line:Startup:Operator:Calendar is required", Validator().Validate(null, options).FailureMessage);

            options.Startup.CheckInOperator = false;
            Assert.True(Validator().Validate(null, options).Succeeded);
        }

        [Fact]
        public void Validator_ReportsTheReplacedProductionOrderPrefix()
        {
            var options = TestLineOptions();
            options.Order.ProductionOrderPrefix = "PO SC";

            Assert.Contains("use \"PO SC.{id}\"", Validator().Validate(null, options).FailureMessage);
        }

        [Fact]
        public void Validator_RejectsAnUnsafeProductionOrderNameFormat()
        {
            var options = TestLineOptions();
            options.Order.ProductionOrderNameFormat = "{id}";

            Assert.Contains("Line:Order:ProductionOrderNameFormat", Validator().Validate(null, options).FailureMessage);
        }

        [Fact]
        public void Validator_RequiresATerminateReasonWhenTerminatingFailedLots()
        {
            var options = TestLineOptions();
            options.FailedLots.TerminateReason = "";

            Assert.Contains("Line:FailedLots:TerminateReason is required", Validator().Validate(null, options).FailureMessage);

            options.FailedLots.Terminate = false;
            Assert.True(Validator().Validate(null, options).Succeeded);
        }

        [Fact]
        public void StartupOptions_ChangeNothingMesWideUnlessTheLineFileSaysSo()
        {
            var startup = new StartupOptions();

            Assert.False(startup.DisableMail);
            Assert.False(startup.DisableTimeConstraints);
            Assert.Empty(startup.DisableDeeActions);
            Assert.False(startup.TerminatePreviousRuns);
            Assert.False(startup.ChangesTheWholeMes);
        }

        [Fact]
        public void Validator_RequiresATerminateReasonWhenTerminatingPreviousRuns()
        {
            var options = TestLineOptions();
            options.Startup.TerminatePreviousRuns = true;
            options.Startup.TerminateReason = "";

            Assert.Contains("Line:Startup:TerminateReason is required", Validator().Validate(null, options).FailureMessage);
        }

        [Fact]
        public void Validator_RequiresTheOrderFlowPaths()
        {
            var options = TestLineOptions();
            options.Order.StartFlowPath = "";

            Assert.Contains("Line:Order:StartFlowPath is required", Validator().Validate(null, options).FailureMessage);
        }
    }

    public class PreviousRunCleanupTests
    {
        [Theory]
        [InlineData("PO SC.4592FB2C", true)]
        [InlineData("PO SC.0001", false)]
        [InlineData("PO SC.4592FB2C.01", false)]
        [InlineData("PO SCX.4592FB2C", false)]
        [InlineData("PO SC.4592fb2c", false)]
        public void IsSimulatorOrder_OnlyMatchesTheNamesTheOrderSourceGives(string orderName, bool expected)
        {
            Assert.Equal(expected, PreviousRunCleanup.IsSimulatorOrder(orderName, "PO SC.{id}"));
        }
    }

    public class ProductionOrderNamingTests
    {
        [Theory]
        [InlineData("PO SC.{id}")]
        [InlineData("SIM-{id}-PO")]
        public void NewName_AlwaysMatchesItsOwnFormat(string format)
        {
            var name = ProductionOrderNaming.NewName(format);

            Assert.True(ProductionOrderNaming.Matches(name, format));
            Assert.DoesNotContain("{id}", name);
        }

        [Theory]
        [InlineData("SIM-4592FB2C-PO", true)]
        [InlineData("SIM-4592FB2C", false)]
        [InlineData("SIM-4592FB2C-PO.01", false)]
        [InlineData("SIM-0001-PO", false)]
        [InlineData(null, false)]
        public void Matches_OnlyTheExactConvention(string? orderName, bool expected)
        {
            Assert.Equal(expected, ProductionOrderNaming.Matches(orderName, "SIM-{id}-PO"));
        }

        [Fact]
        public void QueryPrefix_IsTheTextBeforeTheId()
        {
            Assert.Equal("PO SC.", ProductionOrderNaming.QueryPrefix("PO SC.{id}"));
        }

        [Theory]
        [InlineData("PO SC.{id}", null)]
        [InlineData("", "is required")]
        [InlineData("PO SC", "exactly once")]
        [InlineData("PO {id}.{id}", "exactly once")]
        [InlineData("{id}", "at least 2 characters")]
        [InlineData("P{id}", "at least 2 characters")]
        public void Validate_RejectsFormatsThatCouldMatchOtherOrders(string format, string? expectedError)
        {
            var error = ProductionOrderNaming.Validate(format);

            if (expectedError == null)
            {
                Assert.Null(error);
            }
            else
            {
                Assert.Contains(expectedError, error);
            }
        }
    }

    public class InFlightLotsTests
    {
        [Fact]
        public async Task WhenAllAsync_WaitsForRunsStartedWhileWaiting()
        {
            var inFlight = new InFlightLots();
            var first = new TaskCompletionSource();
            var second = new TaskCompletionSource();
            inFlight.Track(first.Task);

            var waiting = inFlight.WhenAllAsync();
            inFlight.Track(second.Task);
            first.SetResult();
            await Task.Delay(20);
            Assert.False(waiting.IsCompleted);

            second.SetResult();
            await waiting;
            Assert.Equal(0, inFlight.Count);
        }
    }
}
