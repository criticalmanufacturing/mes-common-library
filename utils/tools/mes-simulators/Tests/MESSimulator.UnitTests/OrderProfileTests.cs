using Microsoft.Extensions.Configuration;
using MESSimulator.Line;
using MESSimulator.Pipeline;
using MESSimulator.Steps;
using Xunit;

namespace MESSimulator.UnitTests
{
    /// <summary>
    /// Several products on one line: Line:Order:Products over the defaults of Line:Order, and the weighted pick.
    /// </summary>
    public class OrderProfileTests
    {
        private static OrderOptions Order(params ProductOptions[] products) => new()
        {
            Facility = "Production FE SC",
            LotFlowPath = "LOT:1",
            WaferFlowPath = "WAFER:1",
            StartFlowPath = "START:1",
            DiesPerWafer = 500,
            Characteristics = [new CharacteristicOptions { Name = "Quality", Value = "QA" }],
            Products = [.. products]
        };

        private static LineOptionsValidator Validator() => new([], []);

        /// <summary>The validator's complaints about the order; the empty step list is not the point here.</summary>
        private static List<string> OrderErrors(OrderOptions order, string? onlyAbout = null)
        {
            var options = new LineOptions { Order = order };
            var result = Validator().Validate(null, options);
            return (result.Failures ?? []).Where(f => f.StartsWith("Line:Order") && (onlyAbout == null || f.Contains(onlyAbout))).ToList();
        }

        [Fact]
        public void WithoutProducts_TheOrderIsForTheSingleProduct()
        {
            var order = Order();
            order.Product = "2EDN7524F";

            var profile = Assert.Single(order.Profiles());

            Assert.Equal("2EDN7524F", profile.Product);
            Assert.Equal(1, profile.Weight);
            Assert.Equal(500, profile.DiesPerWafer);
            Assert.Equal("300mm", profile.CapacityClass);
            Assert.Equal("Production", profile.MaterialType);
        }

        [Fact]
        public void AProductListsOnlyWhatDiffersFromTheOrderDefaults()
        {
            var order = Order(
                new ProductOptions { Product = "A" },
                new ProductOptions { Product = "B", DiesPerWafer = 984, CapacityClass = "200mm", MaterialType = "Engineering", Weight = 3 });

            var (a, b) = (order.Profiles()[0], order.Profiles()[1]);

            Assert.Equal((500, "300mm", "Production", 1), (a.DiesPerWafer, a.CapacityClass, a.MaterialType, a.Weight));
            Assert.Equal((984, "200mm", "Engineering", 3), (b.DiesPerWafer, b.CapacityClass, b.MaterialType, b.Weight));
            Assert.Equal("START:1", b.StartFlowPath);
            Assert.Equal("Quality", Assert.Single(b.Characteristics).Name);
        }

        [Fact]
        public void AProductCanHaveItsOwnCharacteristics()
        {
            var order = Order(new ProductOptions { Product = "A", Characteristics = [new CharacteristicOptions { Name = "Coating", Value = "WPR" }] });

            var characteristic = Assert.Single(order.Profiles()[0].Characteristics);

            Assert.Equal(("Coating", "WPR"), (characteristic.Name, characteristic.Value));
        }

        [Fact]
        public void PickProfile_FollowsTheWeights()
        {
            var profiles = Order(
                new ProductOptions { Product = "A", Weight = 1 },
                new ProductOptions { Product = "B", Weight = 3 },
                new ProductOptions { Product = "C", Weight = 0 },
                new ProductOptions { Product = "D", Weight = 2 }).Profiles();

            // Total weight 6: pick 0 is A, 1-3 are B, 4-5 are D
            var picks = Enumerable.Range(0, 6).Select(i => OrderSource.PickProfile(profiles, new FixedRandom(i)).Product);

            Assert.Equal(["A", "B", "B", "B", "D", "D"], picks);
        }

        [Fact]
        public void PickProfile_AskForARandomNumberBelowTheTotalWeight()
        {
            var profiles = Order(new ProductOptions { Product = "A", Weight = 2 }, new ProductOptions { Product = "B", Weight = 5 }).Profiles();
            var random = new FixedRandom(0);

            OrderSource.PickProfile(profiles, random);

            Assert.Equal((0, 7), (random.LastMin, random.LastMax));
        }

        [Fact]
        public void PickProfile_WithNoWeight_Throws()
        {
            var profiles = Order(new ProductOptions { Product = "A", Weight = 0 }).Profiles();

            Assert.Throws<InvalidOperationException>(() => OrderSource.PickProfile(profiles, new FixedRandom(0)));
        }

        [Fact]
        public void Validator_AcceptsSeveralProducts()
        {
            Assert.Empty(OrderErrors(Order(new ProductOptions { Product = "A" }, new ProductOptions { Product = "B", Weight = 0 })));
        }

        [Fact]
        public void Validator_NeedsAProductSomewhere()
        {
            Assert.Contains(OrderErrors(Order()), e => e.Contains("Product is required"));
        }

        [Fact]
        public void Validator_RejectsAProductListedTwice()
        {
            var errors = OrderErrors(Order(new ProductOptions { Product = "A" }, new ProductOptions { Product = "a" }));

            Assert.Contains(errors, e => e.Contains("Products:1") && e.Contains("listed twice"));
        }

        [Fact]
        public void Validator_RejectsAProductWithoutAName()
        {
            Assert.Contains(OrderErrors(Order(new ProductOptions { Product = " " })), e => e.Contains("Products:0:Product is required"));
        }

        [Fact]
        public void Validator_RejectsANegativeWeight()
        {
            Assert.Contains(OrderErrors(Order(new ProductOptions { Product = "A", Weight = -1 })), e => e.Contains("Weight must be >= 0"));
        }

        [Fact]
        public void Validator_RejectsWhenNoProductCanBeLaunched()
        {
            var errors = OrderErrors(Order(new ProductOptions { Product = "A", Weight = 0 }, new ProductOptions { Product = "B", Weight = 0 }));

            Assert.Contains(errors, e => e.Contains("at least one product needs a Weight above 0"));
        }

        [Fact]
        public void Validator_ChecksTheLotQuantityOfEachProduct()
        {
            var errors = OrderErrors(Order(new ProductOptions { Product = "A", MinLotQuantity = 30, MaxLotQuantity = 10 }));

            Assert.Contains(errors, e => e.Contains("Products:0") && e.Contains("lot quantity range [30, 10]"));
        }

        [Fact]
        public void Validator_ChecksTheFlowPathsAfterTheDefaults()
        {
            var order = Order(new ProductOptions { Product = "A", StartFlowPath = "" });
            order.StartFlowPath = "";

            Assert.Contains(OrderErrors(order), e => e.Contains("Products:0:StartFlowPath is required"));
        }

        [Fact]
        public void LineJson_ConfiguresSixProducts_LaunchesTheFourProductionOnes_AndKeepsTheEngineeringOnesOff()
        {
            var profiles = ShippedOrder().Profiles().ToDictionary(p => p.Product);

            Assert.Equal(6, profiles.Count);
            Assert.All(["2EDN7524F", "2EGN7524G", "KommSemi Generic Brand Eice Driver", "KommSemi Generic Brand Gate Driver"],
                p => Assert.Equal(1, profiles[p].Weight));
            // Engineering lots are out of scope (README, "Products")
            Assert.All(["2EDN8524F*", "2EGN7523G*"], p =>
            {
                Assert.Equal(0, profiles[p].Weight);
                Assert.Equal("Engineering", profiles[p].MaterialType);
            });
            Assert.Equal("200mm", profiles["KommSemi Generic Brand Eice Driver"].CapacityClass);
            Assert.Equal("300mm", profiles["2EGN7524G"].CapacityClass);
            Assert.Equal(984, profiles["2EGN7524G"].DiesPerWafer);
        }

        [Fact]
        public void AnEnvironmentVariableCanSwitchAProductOn()
        {
            const string variable = "MESSIM_Line__Order__Products__4__Weight";
            Environment.SetEnvironmentVariable(variable, "2");
            try
            {
                var profiles = ShippedOrder().Profiles();

                Assert.Equal(("2EDN8524F*", 2), (profiles[4].Product, profiles[4].Weight));
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }

        /// <summary>The Line:Order section of the shipped simulationConfiguration.semi.json (copied next to the tests), with the environment applied.</summary>
        private static OrderOptions ShippedOrder() =>
            new ConfigurationBuilder()
                .AddSimulatorSources(AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "simulationConfiguration.semi.json"), [])
                .Build()
                .GetSection(LineOptions.SectionName).Get<LineOptions>()!.Order;

        private sealed class FixedRandom(int value) : IRandomSource
        {
            public int LastMin { get; private set; }
            public int LastMax { get; private set; }

            public int Next(int minInclusive, int maxExclusive)
            {
                (LastMin, LastMax) = (minInclusive, maxExclusive);
                return value;
            }
        }
    }
}
