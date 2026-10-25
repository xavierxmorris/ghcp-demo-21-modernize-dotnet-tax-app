using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TaxApp.Web.Models;

namespace TaxApp.Web.Services
{
    public interface IDeductionRepository
    {
        IList<Deduction> GetAll();

        Deduction Add(string description, DeductionCategory category, decimal amount);

        bool Remove(Guid id);

        decimal Total();
    }

    /// <summary>
    /// Stores deductions as JSON under App_Data. Uses Newtonsoft.Json and HostingEnvironment.MapPath,
    /// two things a framework upgrade has to deal with.
    /// </summary>
    public class DeductionRepository : IDeductionRepository
    {
        private static readonly object Gate = new object();

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() }
        };

        private readonly string _path;
        private readonly string _seedPath;

        public DeductionRepository()
            : this(MapAppData("deductions.json"), MapAppData("deductions.seed.json"))
        {
        }

        public DeductionRepository(string path)
            : this(path, null)
        {
        }

        public DeductionRepository(string path, string seedPath)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A storage path is required.", "path");
            }

            _path = path;
            _seedPath = seedPath;
        }

        public IList<Deduction> GetAll()
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

        public decimal Total()
        {
            return GetAll().Sum(d => d.Amount);
        }

        private static string MapAppData(string fileName)
        {
            var mapped = HostingEnvironment.MapPath("~/App_Data/" + fileName);
            if (mapped == null)
            {
                throw new InvalidOperationException("Not running inside ASP.NET. Pass an explicit storage path.");
            }

            return mapped;
        }

        private List<Deduction> Load()
        {
            if (!File.Exists(_path))
            {
                if (_seedPath != null && File.Exists(_seedPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path));
                    File.Copy(_seedPath, _path);
                }
                else
                {
                    return new List<Deduction>();
                }
            }

            var json = File.ReadAllText(_path);
            return JsonConvert.DeserializeObject<List<Deduction>>(json, JsonSettings) ?? new List<Deduction>();
        }

        private void Save(List<Deduction> items)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, JsonConvert.SerializeObject(items, JsonSettings));
        }
    }
}
