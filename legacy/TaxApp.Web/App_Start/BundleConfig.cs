using System.Web.Optimization;

namespace TaxApp.Web
{
    public static class BundleConfig
    {
        // System.Web.Optimization bundling: a very common thing to find, with no direct equivalent in ASP.NET Core.
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/site").Include(
                "~/Scripts/jquery-3.7.1.min.js",
                "~/Scripts/site.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                "~/Content/site.css"));
        }
    }
}
