using System.Runtime.InteropServices;

namespace TaxApp.Web.Helpers
{
    /// <summary>Shows which runtime is serving the page, so before and after are visible in the UI.</summary>
    public static class RuntimeInfo
    {
        public static string Description
        {
            get { return RuntimeInformation.FrameworkDescription; }
        }
    }
}
