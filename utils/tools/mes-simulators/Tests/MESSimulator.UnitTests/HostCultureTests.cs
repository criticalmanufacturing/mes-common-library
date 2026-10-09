using System.Globalization;
using Xunit;

namespace MESSimulator.UnitTests
{
    public class HostCultureTests
    {
        private static void WithCulture(CultureInfo culture, Action test)
        {
            var (current, ui, defaultCurrent, defaultUi) =
                (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture, CultureInfo.DefaultThreadCurrentCulture, CultureInfo.DefaultThreadCurrentUICulture);
            try
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                test();
            }
            finally
            {
                CultureInfo.CurrentCulture = current;
                CultureInfo.CurrentUICulture = ui;
                CultureInfo.DefaultThreadCurrentCulture = defaultCurrent;
                CultureInfo.DefaultThreadCurrentUICulture = defaultUi;
            }
        }

        [Fact]
        public void EnsureNamedCulture_GivesTheInvariantCultureAName()
        {
            WithCulture(CultureInfo.InvariantCulture, () =>
            {
                Program.EnsureNamedCulture("en-US");

                Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
                Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
                Assert.Equal("en-US", CultureInfo.DefaultThreadCurrentCulture?.Name);
            });
        }

        [Fact]
        public void EnsureNamedCulture_KeepsACultureThatHasAName()
        {
            WithCulture(new CultureInfo("pt-PT"), () =>
            {
                Program.EnsureNamedCulture("en-US");

                Assert.Equal("pt-PT", CultureInfo.CurrentCulture.Name);
            });
        }
    }
}
