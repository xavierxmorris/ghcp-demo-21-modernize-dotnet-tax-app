using System.Configuration;
using System.Globalization;

namespace TaxApp.Web.Services
{
    /// <summary>
    /// Typed access to Web.config appSettings. Read on every call, the way a lot of legacy code does it.
    /// </summary>
    public static class AppSettings
    {
        public static string DefaultTaxYear
        {
            get { return Read("DefaultTaxYear", "2026-27"); }
        }

        public static decimal MedicareLevyRate
        {
            get { return ReadDecimal("MedicareLevyRate", 0.02m); }
        }

        public static decimal GstRate
        {
            get { return ReadDecimal("GstRate", 0.10m); }
        }

        private static string Read(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static decimal ReadDecimal(string key, decimal fallback)
        {
            decimal parsed;
            var raw = ConfigurationManager.AppSettings[key];
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }
    }
}
