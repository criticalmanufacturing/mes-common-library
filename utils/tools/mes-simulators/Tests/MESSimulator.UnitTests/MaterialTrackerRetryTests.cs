using MESSimulator.Steps;
using Xunit;

namespace MESSimulator.UnitTests
{
    public class MaterialTrackerRetryTests
    {
        private const string Stale = "The data for object Sorter_Asyst-001 of type Resource has changed since last viewed. Please refresh the object.";

        [Fact]
        public void WithFreshResource_ReadsAndCallsAgainWhileTheMesSaysTheDataChanged()
        {
            int attempts = 0;

            var result = MaterialTracker.WithFreshResource(() =>
            {
                attempts++;
                return attempts < 3 ? throw new InvalidOperationException(Stale) : "tracked out";
            }, attempts: 3);

            Assert.Equal(("tracked out", 3), (result, attempts));
        }

        [Fact]
        public void WithFreshResource_GivesUpAfterTheAttempts()
        {
            int attempts = 0;

            Assert.Throws<InvalidOperationException>(() => MaterialTracker.WithFreshResource<string>(() =>
            {
                attempts++;
                throw new InvalidOperationException(Stale);
            }, attempts: 3));

            Assert.Equal(3, attempts);
        }

        [Fact]
        public void WithFreshResource_DoesNotRepeatAnyOtherFailure()
        {
            int attempts = 0;

            Assert.Throws<InvalidOperationException>(() => MaterialTracker.WithFreshResource<string>(() =>
            {
                attempts++;
                throw new InvalidOperationException("Material is not Processed.");
            }, attempts: 3));

            Assert.Equal(1, attempts);
        }

        [Fact]
        public void WithFreshResource_TellsEachRetry()
        {
            int attempts = 0;
            var retries = new List<int>();

            MaterialTracker.WithFreshResource(() => ++attempts < 3 ? throw new InvalidOperationException(Stale) : 0, 3, (_, attempt) => retries.Add(attempt));

            Assert.Equal([1, 2], retries);
        }
    }
}
