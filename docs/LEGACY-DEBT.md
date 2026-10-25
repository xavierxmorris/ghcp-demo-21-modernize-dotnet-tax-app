# Legacy debt register (the answer key)

> **For the presenter. Keep this out of the workspace the agent sees**: `./go.ps1 -Reset` copies only `legacy/`, so this file is
> never in front of the agent. Fictional demo; illustrative tax figures.

What a good assessment of `legacy/TaxApp.Web` should surface, and what the prebaked reference in `modern/` did about each item.
"Agent will" is a **prediction to rehearse**, not a promise: the agent's choices vary from run to run.

## Debt register

| # | Where | Legacy pattern | Why it blocks or matters | What `modern/` does | Agent will (rehearse) |
| --- | --- | --- | --- | --- | --- |
| 1 | `TaxApp.Web.csproj` | `net48`, `OutputType=Library`, output redirected to `bin\` so ASP.NET can load it | There is no `System.Web` on modern .NET | `Microsoft.NET.Sdk.Web`, `net10.0`, executable web app | Retarget, change SDK |
| 2 | `Global.asax`, `Global.asax.cs` | `HttpApplication`, `Application_Start`, `Application_Error` | Replaced by `Program.cs` and middleware | `Program.cs` with `WebApplication` and `UseExceptionHandler` | Convert (skill: Upgrading Global.asax) |
| 3 | `App_Start/RouteConfig.cs`, `FilterConfig.cs` | `RouteCollection`, `GlobalFilterCollection`, `HandleErrorAttribute` | Different routing and filter model | `MapControllerRoute`, exception handler | Convert |
| 4 | `App_Start/BundleConfig.cs`, `_Layout.cshtml` | `System.Web.Optimization` (`ScriptBundle`, `Scripts.Render`) | No equivalent package for ASP.NET Core | Plain `<link>` and `<script>` tags with `asp-append-version` | Replace bundling |
| 5 | `Web.config` | `appSettings` read with `ConfigurationManager`; `system.web`, `system.webServer`, binding redirects | Config model changed | `appsettings.json` and the options pattern (`TaxOptions`) | Move settings |
| 6 | `Services/AppSettings.cs` | Static `ConfigurationManager` access on every call | Hidden global state, not available as-is | `IOptions<TaxOptions>` injected | Replace or adapt |
| 7 | `Services/DeductionRepository.cs` | `HostingEnvironment.MapPath("~/App_Data/…")` | `System.Web.Hosting` is gone | `IWebHostEnvironment.ContentRootPath` | Replace |
| 8 | `Services/DeductionRepository.cs` | Newtonsoft.Json with `StringEnumConverter` | Learn's own custom-instruction example is "replace Newtonsoft with System.Text.Json" | `System.Text.Json`, **same file format** | Keep or replace (check the file format) |
| 9 | `Controllers/TaxController.cs` | `JsonResult` with `JsonRequestBehavior.AllowGet`; `HttpStatusCodeResult` | Neither exists in ASP.NET Core MVC | `Json(...)` and `BadRequest()`, PascalCase naming kept | Convert, and watch the JSON casing |
| 10 | `Models/*.cs` | `System.Web.Mvc.SelectListItem` inside models | MVC types move to `Microsoft.AspNetCore.Mvc.Rendering` | Same idea, new namespace | Convert |
| 11 | `Controllers/*.cs` | Parameterless constructors chaining to concrete dependencies ("poor man's DI") | Works, but not how ASP.NET Core wires things | Constructor injection with DI registrations | Optional modernisation |
| 12 | `Views/**` | Razor with `@Html.*` helpers, `@Scripts.Render`, `Views/Web.config` namespaces | `_ViewImports.cshtml` replaces `Views/Web.config`; most helpers still exist | Same views, plus `_ViewImports` and tag-helper opt-in | Convert (skills: Razor views) |
| 13 | `Global.asax.cs`, controllers | log4net with a file path set through `GlobalContext` | No built-in hook; the framework has `ILogger` | `ILogger<T>` | Offer to migrate (skill: Upgrading MVC logging) |
| 14 | `Scripts/site.js` | Reads **PascalCase** JSON property names | ASP.NET Core serialises camelCase by default | Naming policy set to `null` | Watch this one |
| 15 | `TaxApp.Tests` | xUnit on `net48` | Must retarget with the app | xUnit on `net10.0`, plus in-memory HTTP tests | Retarget |
| 16 | `Models/*.cs` | No nullable reference types, DataAnnotations `[Range]` | ASP.NET Core treats non-nullable reference types as required by default | `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true` | Watch validation messages |
| 17 | `Helpers/MoneyFormatter.cs` | `new CultureInfo("en-AU")` and `decimal` formatting | Culture data can differ by runtime or ICU | Kept; machine checks read culture-neutral `data-value` attributes | Usually fine |
| 18 | `Content/`, `Scripts/`, `App_Data/` | Static folders next to the code | Learn describes the *Upgrading MVC static files* skill as moving `Content/`, `Scripts/` **and `App_Data/`** into `wwwroot/`. Everything under `wwwroot` is served publicly, so moving `App_Data` would publish the deductions file | `Content` and `Scripts` become `wwwroot/css` and `wwwroot/js`; `App_Data` stays outside `wwwroot` | **Watch this one**: the contract checks `/App_Data/…` is not served |

## Things that must not change (the contract)

These are what the golden master and the HTTP contract protect, and what `.github/skills/tax-money-rules` asks the agent to respect:

- Rounding: `Math.Round(value, 2, MidpointRounding.AwayFromZero)` on each component. Round-to-even breaks two golden vectors.
- URLs, action names, form field names, `data-testid` and `data-value` attributes.
- PascalCase JSON from `/Tax/EstimateJson`.
- The `App_Data/deductions.json` file format (PascalCase names, enum values as strings), so a file written before the upgrade still loads.

## Behaviour differences that were checked

These were observed on this machine (2026-10-26) running the same contract against both apps. They are why the contract asserts
"rejected, nothing saved" and not an exact status code:

| Situation | Legacy (MVC 5, .NET Framework 4.8) | Modern (ASP.NET Core, .NET 10) |
| --- | --- | --- |
| POST without an anti-forgery token | HTTP **500** (`HttpAntiForgeryException`) | HTTP **400** |
| POST with HTML-looking text such as `<b>` in a form field | HTTP **500** (request validation: potentially dangerous `Request.Form` value); nothing saved | HTTP 302; saved and **HTML-encoded** when rendered |
| `Html.BeginForm` for a POST form | Needs an explicit `@Html.AntiForgeryToken()` | Adds the token itself (the reference views omit the explicit call and the contract still finds and accepts a token) |

## About the legacy host (so nothing surprises you)

`tools/LegacyHost` runs the real System.Web pipeline in-process and is **classic pipeline mode**, not IIS integrated mode. It
needed three things beyond a naive host, all found by running the app or the contract. The app is not modified for any of them:

1. A `MapPath` that maps virtual paths to the site folder (static files were 404 without it).
2. Switching off the anti-forgery helper's `X-Frame-Options` write, which needs `Response.Headers` (integrated mode only).
3. Hiding `bin`, `App_Data`, `App_Code` and the other special folders (HTTP 404, as IIS request filtering does). Without this the
   host served `/App_Data/deductions.seed.json` with HTTP 200; the contract's "is not publicly served" checks caught it.
