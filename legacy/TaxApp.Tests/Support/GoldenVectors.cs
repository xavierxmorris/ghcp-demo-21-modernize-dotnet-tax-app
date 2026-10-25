using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TaxApp.Tests.Support
{
    /// <summary>Reads Golden/tax-vectors.json, the golden master shared with the modern app and the HTTP contract script.</summary>
    public static class GoldenVectors
    {
        private static readonly Lazy<JObject> Document = new Lazy<JObject>(() =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Golden", "tax-vectors.json");
            return JObject.Parse(File.ReadAllText(path));
        });

        public static IEnumerable<object[]> EstimateNames()
        {
            return Names("estimate");
        }

        public static IEnumerable<object[]> GstNames()
        {
            return Names("gst");
        }

        public static IEnumerable<object[]> ReturnNames()
        {
            return Names("return");
        }

        public static JObject Estimate(string name)
        {
            return Find("estimate", name);
        }

        public static JObject Gst(string name)
        {
            return Find("gst", name);
        }

        public static JObject Return(string name)
        {
            return Find("return", name);
        }

        public static decimal Money(JToken token)
        {
            return decimal.Parse((string)token, CultureInfo.InvariantCulture);
        }

        private static IEnumerable<object[]> Names(string section)
        {
            return ((JArray)Document.Value[section]).Select(c => new object[] { (string)c["name"] }).ToList();
        }

        private static JObject Find(string section, string name)
        {
            return ((JArray)Document.Value[section]).Cast<JObject>().Single(c => (string)c["name"] == name);
        }
    }
}
