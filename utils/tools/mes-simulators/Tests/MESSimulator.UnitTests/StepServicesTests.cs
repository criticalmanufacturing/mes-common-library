using MESSimulator.Steps;
using MESSimulator.Steps.Actions;
using Xunit;

namespace MESSimulator.UnitTests
{
    public class SplitIntoRandomGroupsTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(25)]
        public void Split_CoversEveryWaferOnce_InNonEmptyGroups_WithinTheMaximum(int waferCount)
        {
            var wafers = Enumerable.Range(1, waferCount).ToList();

            for (int seed = 0; seed < 50; seed++)
            {
                var groups = SplitTrackOutAction.SplitIntoRandomGroups(wafers, maxGroups: 4, new RandomSource(new Random(seed)));

                Assert.InRange(groups.Count, 1, Math.Min(4, waferCount));
                Assert.All(groups, g => Assert.NotEmpty(g));
                Assert.Equal(wafers, groups.SelectMany(g => g).Order());
            }
        }

        [Fact]
        public void Split_IsReproducible_WithTheSameSeed()
        {
            var wafers = Enumerable.Range(1, 20).ToList();

            var first = SplitTrackOutAction.SplitIntoRandomGroups(wafers, 4, new RandomSource(new Random(42)));
            var second = SplitTrackOutAction.SplitIntoRandomGroups(wafers, 4, new RandomSource(new Random(42)));

            Assert.Equal(first, second);
        }

        [Fact]
        public void Split_OfNoWafers_GivesNoGroups()
        {
            Assert.Empty(SplitTrackOutAction.SplitIntoRandomGroups(new List<int>(), 4, new RandomSource(new Random(1))));
        }
    }

    public class ReworkChaosTests
    {
        [Theory]
        [InlineData(10, 0.5, 5)]
        [InlineData(3, 0.5, 1)]
        [InlineData(1, 0.1, 1)]
        [InlineData(20, 1.0, 20)]
        public void WafersToScrap_IsBetweenOneAndTheMaxFraction(int wafers, double maxFraction, int max)
        {
            var random = new RandomSource(new Random(3));
            for (int i = 0; i < 200; i++)
            {
                int count = ReworkChaos.WafersToScrap(wafers, (decimal)maxFraction, random);
                Assert.InRange(count, 1, max);
            }
        }

        [Fact]
        public void WafersToScrap_IsZeroForALotWithoutWafers()
        {
            Assert.Equal(0, ReworkChaos.WafersToScrap(0, 0.5m, new FirstValueRandom()));
        }
    }

    public class ResourceCatalogTests
    {
        [Fact]
        public void PickResource_ReturnsOneOfTheStepResources()
        {
            var catalog = new ResourceCatalog(StepTestFactory.TestLine(), new FakeStepResources(), new RandomSource(new Random(7)));

            Assert.Contains(catalog.PickResource("COAT"), new[] { "SUSS Coat-001", "SUSS Coat-002" });
        }

        [Fact]
        public void PickResource_FailsForAStepNotInTheLine()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new ResourceCatalog(StepTestFactory.TestLine(), new FakeStepResources(), new FirstValueRandom()).PickResource("NOT A STEP"));
            Assert.Contains("'NOT A STEP' is not in simulationConfiguration.json", ex.Message);
        }

        [Fact]
        public void HasResources_IsFalseForAStepWithAnEmptyResourceList()
        {
            var catalog = new ResourceCatalog(StepTestFactory.TestLine(), new FakeStepResources(), new FirstValueRandom());

            Assert.False(catalog.HasResources("AOI"));
            Assert.True(catalog.HasResources("coat"));
        }

        [Fact]
        public void ResourcesFor_ComesFromTheMes_WhenLineJsonListsNone_LookedUpOnce()
        {
            var mes = new FakeStepResources();
            mes.ByStep["AOI"] = ["Camtek Falcon 530", "Camtek Falcon 531"];
            var catalog = new ResourceCatalog(StepTestFactory.TestLine(), mes, new FirstValueRandom());

            Assert.Equal(["Camtek Falcon 530", "Camtek Falcon 531"], catalog.ResourcesFor("AOI"));
            Assert.Equal("Camtek Falcon 530", catalog.PickResource("aoi"));
            Assert.Equal(1, mes.Lookups);
        }

        [Fact]
        public void ResourcesFor_LineJsonRestrictsTheStep_OverTheMes()
        {
            var mes = new FakeStepResources();
            mes.ByStep["COAT"] = ["SUSS Coat-001", "SUSS Coat-002", "SUSS Coat-003"];
            var catalog = new ResourceCatalog(StepTestFactory.TestLine(), mes, new FirstValueRandom());

            Assert.Equal(["SUSS Coat-001", "SUSS Coat-002"], catalog.ResourcesFor("COAT"));
            Assert.Equal(0, mes.Lookups);
        }

        [Fact]
        public void AllResources_ListsEachResourceOnce()
        {
            var catalog = new ResourceCatalog(StepTestFactory.TestLine(), new FakeStepResources(), new FirstValueRandom());

            Assert.Equal(catalog.AllResources.Count, catalog.AllResources.Distinct().Count());
            Assert.Contains("Koyo VF-5900A", catalog.AllResources);
        }
    }

    public class ResourceLocksTests
    {
        [Fact]
        public async Task AcquireAsync_SerializesTheSameResource()
        {
            var locks = new ResourceLocks();
            var first = await locks.AcquireAsync("SUSS Coat-001");

            var second = locks.AcquireAsync("SUSS Coat-001");
            await Task.Delay(50);
            Assert.False(second.IsCompleted);

            first.Dispose();
            (await second).Dispose();
        }

        [Fact]
        public async Task AcquireAsync_DoesNotBlockOtherResources()
        {
            var locks = new ResourceLocks();
            using var coat = await locks.AcquireAsync("SUSS Coat-001");

            var developer = locks.AcquireAsync("SUSS Devs-001");

            Assert.True(developer.IsCompleted);
            (await developer).Dispose();
        }

        [Fact]
        public async Task Dispose_IsSafeToCallTwice()
        {
            var locks = new ResourceLocks();
            var lease = await locks.AcquireAsync("SUSS Coat-001");
            lease.Dispose();
            lease.Dispose();

            using var again = await locks.AcquireAsync("SUSS Coat-001");
            var blocked = locks.AcquireAsync("SUSS Coat-001");
            await Task.Delay(50);
            Assert.False(blocked.IsCompleted);
            again.Dispose();
            (await blocked).Dispose();
        }
    }
}
