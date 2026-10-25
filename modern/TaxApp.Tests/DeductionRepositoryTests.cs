using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Tests;

public sealed class DeductionRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "taxapp-modern-tests-" + Guid.NewGuid().ToString("N"));

    private string StorePath => Path.Combine(_directory, "deductions.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Fact]
    public void A_missing_file_means_no_deductions()
    {
        var repository = new JsonFileDeductionRepository(StorePath);

        Assert.Empty(repository.GetAll());
        Assert.Equal(0m, repository.Total());
    }

    [Fact]
    public void Added_deductions_are_saved_and_summed()
    {
        var repository = new JsonFileDeductionRepository(StorePath);

        repository.Add("  Work boots  ", DeductionCategory.ToolsAndEquipment, 120.456m);
        repository.Add("Course", DeductionCategory.SelfEducation, 500m);

        var items = new JsonFileDeductionRepository(StorePath).GetAll();
        Assert.Equal(2, items.Count);
        Assert.Equal("Work boots", items[0].Description);
        Assert.Equal(120.46m, items[0].Amount);
        Assert.Equal(620.46m, repository.Total());
    }

    [Fact]
    public void Remove_deletes_only_the_matching_deduction()
    {
        var repository = new JsonFileDeductionRepository(StorePath);
        var keep = repository.Add("Keep", DeductionCategory.Other, 10m);
        var drop = repository.Add("Drop", DeductionCategory.Other, 20m);

        Assert.True(repository.Remove(drop.Id));
        Assert.False(repository.Remove(drop.Id));

        Assert.Equal([keep.Id], repository.GetAll().Select(d => d.Id));
    }

    [Fact]
    public void The_file_format_is_part_of_the_contract()
    {
        var repository = new JsonFileDeductionRepository(StorePath);
        repository.Add("Desk", DeductionCategory.HomeOffice, 99.5m);

        var json = File.ReadAllText(StorePath);

        // Same as the legacy app: PascalCase property names and enum names as strings.
        Assert.Contains("\"Description\": \"Desk\"", json);
        Assert.Contains("\"Category\": \"HomeOffice\"", json);
        Assert.Contains("\"Amount\": 99.5", json);
    }

    [Fact]
    public void The_seed_file_is_copied_on_first_use()
    {
        Directory.CreateDirectory(_directory);
        var seedPath = Path.Combine(_directory, "deductions.seed.json");
        File.WriteAllText(seedPath,
            """[{"Id":"3f2b8c1e-6b1a-4c7e-9d11-0a5e2c7b1001","Description":"Seed","Category":"Other","Amount":12.5,"AddedUtc":"2026-07-01T00:00:00Z"}]""");

        var repository = new JsonFileDeductionRepository(StorePath, seedPath);

        Assert.Equal(12.5m, repository.Total());
        Assert.True(File.Exists(StorePath));
    }

    [Fact]
    public void A_file_written_by_the_legacy_app_can_be_read()
    {
        // This is what Newtonsoft.Json produced in the legacy repository, byte for byte in shape.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(StorePath, """
            [
              {
                "Id": "3f2b8c1e-6b1a-4c7e-9d11-0a5e2c7b1002",
                "Description": "Home office running costs",
                "Category": "HomeOffice",
                "Amount": 780.5,
                "AddedUtc": "2026-07-02T00:00:00Z"
              }
            ]
            """);

        var item = Assert.Single(new JsonFileDeductionRepository(StorePath).GetAll());

        Assert.Equal(DeductionCategory.HomeOffice, item.Category);
        Assert.Equal(780.5m, item.Amount);
        Assert.Equal(DateTimeKind.Utc, item.AddedUtc.Kind);
    }
}
