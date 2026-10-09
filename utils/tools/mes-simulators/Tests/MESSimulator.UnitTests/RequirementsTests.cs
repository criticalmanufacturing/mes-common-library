using MESSimulator.Line;
using MESSimulator.Pipeline;
using MESSimulator.Steps;
using MESSimulator.Steps.Actions;
using Xunit;

namespace MESSimulator.UnitTests
{
    /// <summary>
    /// A finished good is only launched when the stock of its semi-finished goods covers it (Line:Order:Products:Requires).
    /// </summary>
    public class RequirementsTests
    {
        private static readonly List<RequirementOptions> RooftopUnit =
        [
            new() { Product = "RTU-LATERAL C", QuantityPerUnit = 2 },
            new() { Product = "VFD-10HP", QuantityPerUnit = 1 }
        ];

        [Fact]
        public void UnitsCovered_IsTheScarcestRequirement_RoundedDown()
        {
            var available = new Dictionary<string, decimal> { ["RTU-LATERAL C"] = 7, ["VFD-10HP"] = 5 };

            Assert.Equal(3, OrderSource.UnitsCovered(RooftopUnit, available));
        }

        [Fact]
        public void UnitsCovered_IsZero_WhenARequiredProductHasNoStock()
        {
            var available = new Dictionary<string, decimal> { ["RTU-LATERAL C"] = 40 };

            Assert.Equal(0, OrderSource.UnitsCovered(RooftopUnit, available));
        }

        [Fact]
        public void UnitsCovered_IgnoresStockPromisedBeyondWhatIsThere()
        {
            // More reserved than in stock: nothing is available, not a negative amount
            var available = new Dictionary<string, decimal> { ["RTU-LATERAL C"] = -4, ["VFD-10HP"] = 5 };

            Assert.Equal(0, OrderSource.UnitsCovered(RooftopUnit, available));
        }

        [Fact]
        public void UnitsCovered_IsUnlimited_WithoutRequirements()
        {
            Assert.Equal(int.MaxValue, OrderSource.UnitsCovered([], new Dictionary<string, decimal>()));
        }

        [Fact]
        public void Profiles_CarryTheRequirements_OthersHaveNone()
        {
            var order = new OrderOptions
            {
                Products =
                [
                    new ProductOptions { Product = "Rooftop Unit - HVAC RTU", Requires = RooftopUnit, RequiresAtStep = "Kanban Final Assembly" },
                    new ProductOptions { Product = "VFD-10HP" }
                ]
            };

            var profiles = order.Profiles();

            Assert.Equal(2, profiles[0].Requires.Count);
            Assert.Equal("Kanban Final Assembly", profiles[0].RequiresAtStep);
            Assert.Empty(profiles[1].Requires);
        }

        [Fact]
        public void Validator_RequiresTheStockStep_AndPositiveQuantities()
        {
            var options = new LineOptions
            {
                Order = new OrderOptions
                {
                    Facility = "Production InduTech",
                    LotFlowPath = "Final Assembly:A:1/BASE ASM:1",
                    StartFlowPath = "Final Assembly:A:1/BASE ASM:1",
                    DiscreteProduct = true,
                    Products =
                    [
                        new ProductOptions { Product = "Rooftop Unit - HVAC RTU", Requires = [new() { Product = "VFD-10HP", QuantityPerUnit = 0 }] }
                    ]
                },
                Steps = new(StringComparer.OrdinalIgnoreCase) { ["BASE ASM"] = new StepOptions() }
            };

            var result = new LineOptionsValidator([], []).Validate(null, options);

            Assert.Contains("Line:Order:Products:0:RequiresAtStep is required with Requires.", result.FailureMessage);
            Assert.Contains("Line:Order:Products:0:Requires:VFD-10HP: QuantityPerUnit must be > 0.", result.FailureMessage);
        }

        [Fact]
        public void Validator_RequiresAReplenishFlowPath_AndAQuantityThatCoversTheLargestOrder()
        {
            var options = new LineOptions
            {
                Order = new OrderOptions
                {
                    Facility = "Production InduTech",
                    LotFlowPath = "Final Assembly:A:1/BASE ASM:1",
                    StartFlowPath = "Final Assembly:A:1/BASE ASM:1",
                    DiscreteProduct = true,
                    MinLotQuantity = 1,
                    MaxLotQuantity = 2,
                    Products =
                    [
                        new ProductOptions
                        {
                            Product = "Rooftop Unit - HVAC RTU",
                            RequiresAtStep = "Kanban Final Assembly",
                            Requires = [new() { Product = "Roof 10T HVAC", QuantityPerUnit = 1, Replenish = new ReplenishOptions { FlowPath = "", Quantity = 1 } }]
                        }
                    ]
                },
                Steps = new(StringComparer.OrdinalIgnoreCase) { ["BASE ASM"] = new StepOptions() }
            };

            var result = new LineOptionsValidator([], []).Validate(null, options);

            Assert.Contains("Requires:Roof 10T HVAC:Replenish:FlowPath is required.", result.FailureMessage);
            Assert.Contains("Requires:Roof 10T HVAC:Replenish:Quantity must cover the largest order (2).", result.FailureMessage);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(15)]
        public void LotSplitQuantities_AddUpToTheLot_InAtMostFourWholeParts(int quantity)
        {
            var parts = LotSplitTrackOutAction.SplitQuantities(quantity, 4, new RandomSource(new Random(42)));

            Assert.Equal(quantity, parts.Sum());
            Assert.InRange(parts.Count, 1, Math.Min(4, quantity));
            Assert.All(parts, p => Assert.True(p >= 1));
        }

        [Theory]
        [InlineData(0, 114)]
        [InlineData(1000, 120)]
        [InlineData(2000, 126)]
        public void DataCollectionReading_StaysWithinTheVariation(int permille, decimal expected)
        {
            Assert.Equal(expected, DataCollectionAction.Reading(120, 0.05m, permille));
        }
    }
}
