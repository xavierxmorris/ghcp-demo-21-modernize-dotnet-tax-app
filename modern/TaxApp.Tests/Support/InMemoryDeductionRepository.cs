using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Tests.Support;

public sealed class InMemoryDeductionRepository : IDeductionRepository
{
    private readonly List<Deduction> _items = [];

    public InMemoryDeductionRepository(decimal seededTotal = 0m)
    {
        if (seededTotal > 0m)
        {
            _items.Add(new Deduction
            {
                Id = Guid.NewGuid(),
                Description = "Seeded",
                Category = DeductionCategory.Other,
                Amount = seededTotal,
                AddedUtc = DateTime.UtcNow
            });
        }
    }

    public IReadOnlyList<Deduction> GetAll() => _items.ToList();

    public Deduction Add(string description, DeductionCategory category, decimal amount)
    {
        var deduction = new Deduction
        {
            Id = Guid.NewGuid(),
            Description = description,
            Category = category,
            Amount = amount,
            AddedUtc = DateTime.UtcNow
        };
        _items.Add(deduction);
        return deduction;
    }

    public bool Remove(Guid id) => _items.RemoveAll(d => d.Id == id) > 0;

    public decimal Total() => _items.Sum(d => d.Amount);
}
