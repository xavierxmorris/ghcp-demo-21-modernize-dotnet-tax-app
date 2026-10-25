using System;
using System.Collections.Generic;
using System.Linq;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Tests.Support
{
    public class InMemoryDeductionRepository : IDeductionRepository
    {
        private readonly List<Deduction> _items = new List<Deduction>();

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

        public IList<Deduction> GetAll()
        {
            return _items.ToList();
        }

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

        public bool Remove(Guid id)
        {
            return _items.RemoveAll(d => d.Id == id) > 0;
        }

        public decimal Total()
        {
            return _items.Sum(d => d.Amount);
        }
    }
}
