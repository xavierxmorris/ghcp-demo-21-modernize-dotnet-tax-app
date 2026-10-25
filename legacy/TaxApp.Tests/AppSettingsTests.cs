using TaxApp.Web.Services;
using Xunit;

namespace TaxApp.Tests
{
    /// <summary>
    /// Web.config is not loaded in a unit-test AppDomain, so these show the built-in fallbacks.
    /// This is exactly the kind of hidden coupling a framework upgrade surfaces.
    /// </summary>
    public class AppSettingsTests
    {
        [Fact]
        public void Defaults_apply_when_the_settings_are_missing()
        {
            Assert.Equal("2026-27", AppSettings.DefaultTaxYear);
            Assert.Equal(0.02m, AppSettings.MedicareLevyRate);
            Assert.Equal(0.10m, AppSettings.GstRate);
        }
    }
}
