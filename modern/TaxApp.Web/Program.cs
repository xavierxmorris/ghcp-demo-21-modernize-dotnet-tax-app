using TaxApp.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllersWithViews(options =>
    {
        // ASP.NET Core would otherwise treat every non-nullable reference type as [Required].
        // The legacy app never did, so keep validation exactly as it was.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        // Keep the legacy wire contract: PascalCase property names (the estimator's JavaScript reads them).
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.Configure<TaxOptions>(builder.Configuration.GetSection(TaxOptions.SectionName));
builder.Services.AddSingleton<ITaxCalculator, TaxCalculator>();
builder.Services.AddSingleton<IGstCalculator, GstCalculator>();
builder.Services.AddSingleton<IDeductionRepository, JsonFileDeductionRepository>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Local demo runs over plain HTTP. Terminate TLS (and add HTTPS redirection and HSTS) at your real host.
app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program;
