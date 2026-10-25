using System;
using System.IO;
using System.Linq;
using TaxApp.Web.Models;
using TaxApp.Web.Services;
using Xunit;

namespace TaxApp.Tests
{
    public sealed class DeductionRepositoryTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "taxapp-tests-" + Guid.NewGuid().ToString("N"));

        private string StorePath
        {
            get { return Path.Combine(_directory, "deductions.json"); }
        }

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
            var repository = new DeductionRepository(StorePath);

            Assert.Empty(repository.GetAll());
            Assert.Equal(0m, repository.Total());
        }

        [Fact]
        public void Added_deductions_are_saved_and_summed()
        {
            var repository = new DeductionRepository(StorePath);

            repository.Add("  Work boots  ", DeductionCategory.ToolsAndEquipment, 120.456m);
            repository.Add("Course", DeductionCategory.SelfEducation, 500m);

            var items = new DeductionRepository(StorePath).GetAll();
            Assert.Equal(2, items.Count);
            Assert.Equal("Work boots", items[0].Description);
            Assert.Equal(120.46m, items[0].Amount);
            Assert.Equal(620.46m, repository.Total());
        }

        [Fact]
        public void Remove_deletes_only_the_matching_deduction()
        {
            var repository = new DeductionRepository(StorePath);
            var keep = repository.Add("Keep", DeductionCategory.Other, 10m);
            var drop = repository.Add("Drop", DeductionCategory.Other, 20m);

            Assert.True(repository.Remove(drop.Id));
            Assert.False(repository.Remove(drop.Id));

            Assert.Equal(new[] { keep.Id }, repository.GetAll().Select(d => d.Id).ToArray());
        }

        [Fact]
        public void The_file_format_is_part_of_the_contract()
        {
            var repository = new DeductionRepository(StorePath);
            repository.Add("Desk", DeductionCategory.HomeOffice, 99.5m);

            var json = File.ReadAllText(StorePath);

            // PascalCase property names and enum names as strings: the modern app must read the same file.
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
                "[{\"Id\":\"3f2b8c1e-6b1a-4c7e-9d11-0a5e2c7b1001\",\"Description\":\"Seed\",\"Category\":\"Other\",\"Amount\":12.5,\"AddedUtc\":\"2026-07-01T00:00:00Z\"}]");

            var repository = new DeductionRepository(StorePath, seedPath);

            Assert.Equal(12.5m, repository.Total());
            Assert.True(File.Exists(StorePath));
        }

        [Fact]
        public void The_default_constructor_needs_ASP_NET_hosting()
        {
            Assert.Throws<InvalidOperationException>(() => new DeductionRepository());
        }
    }
}
