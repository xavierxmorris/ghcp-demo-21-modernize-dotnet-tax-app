using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TaxApp.Web.Models;

namespace TaxApp.Web.Services;

public interface IDeductionRepository
{
    IReadOnlyList<Deduction> GetAll();

    Deduction Add(string description, DeductionCategory category, decimal amount);

    bool Remove(Guid id);

    decimal Total();
}

/// <summary>
/// Stores deductions as JSON under App_Data, in exactly the file format the legacy app wrote
/// (PascalCase names, enums as strings), so either app can read the other's file.
/// </summary>
public sealed class JsonFileDeductionRepository : IDeductionRepository
{
    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly string? _seedPath;

    public JsonFileDeductionRepository(IOptions<TaxOptions> options, IWebHostEnvironment environment)
        : this(
            Path.Combine(environment.ContentRootPath, options.Value.DeductionsPath),
            Path.Combine(environment.ContentRootPath, options.Value.DeductionsSeedPath))
    {
    }

    public JsonFileDeductionRepository(string path, string? seedPath = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _path = path;
        _seedPath = seedPath;
    }

    public IReadOnlyList<Deduction> GetAll()
    {
        lock (Gate)
        {
            return Load();
        }
    }

    public Deduction Add(string description, DeductionCategory category, decimal amount)
    {
        lock (Gate)
        {
            var items = Load();
            var deduction = new Deduction
            {
                Id = Guid.NewGuid(),
                Description = description.Trim(),
                Category = category,
                Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
                AddedUtc = DateTime.UtcNow
            };
            items.Add(deduction);
            Save(items);
            return deduction;
        }
    }

    public bool Remove(Guid id)
    {
        lock (Gate)
        {
            var items = Load();
            var removed = items.RemoveAll(d => d.Id == id) > 0;
            if (removed)
            {
                Save(items);
            }

            return removed;
        }
    }

    public decimal Total() => GetAll().Sum(d => d.Amount);

    private List<Deduction> Load()
    {
        if (!File.Exists(_path))
        {
            if (_seedPath is not null && File.Exists(_seedPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.Copy(_seedPath, _path);
            }
            else
            {
                return [];
            }
        }

        return JsonSerializer.Deserialize<List<Deduction>>(File.ReadAllText(_path), JsonOptions) ?? [];
    }

    private void Save(List<Deduction> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, JsonOptions));
    }
}
