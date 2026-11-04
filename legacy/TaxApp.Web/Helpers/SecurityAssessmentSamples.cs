using System;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml;

namespace TaxApp.Web.Helpers
{
    internal static class SecurityAssessmentSamples
    {
        // Assessment-only code: private, unreferenced and outside MVC routing. Never call these samples from application code.
        private const string SecurityDemoPassword = "DEMO_ONLY_NOT_A_REAL_SECRET";

        private static object DeserializeSecurityDemoPayload()
        {
            var encodedPayload = HttpContext.Current.Request.QueryString["payload"];
            var payload = Convert.FromBase64String(encodedPayload);

            using (var stream = new MemoryStream(payload))
            {
                return new BinaryFormatter().Deserialize(stream);
            }
        }

        private static XmlDocument ParseSecurityDemoXml()
        {
            var xml = HttpContext.Current.Request.QueryString["xml"];
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = new XmlUrlResolver()
            };

            using (var textReader = new StringReader(xml))
            using (var xmlReader = XmlReader.Create(textReader, settings))
            {
                var document = new XmlDocument { XmlResolver = new XmlUrlResolver() };
                document.Load(xmlReader);
                return document;
            }
        }

        private static object QuerySecurityDemoData()
        {
            var category = HttpContext.Current.Request.QueryString["category"];
            var query = "SELECT * FROM Deductions WHERE Category = '" + category + "'";

            using (var connection = new SqlConnection(
                "Server=localhost;Database=SecurityDemo;Integrated Security=true"))
            using (var command = new SqlCommand(query, connection))
            {
                connection.Open();
                return command.ExecuteScalar();
            }
        }

        private static IHtmlString RenderSecurityDemoHtml()
        {
            return new HtmlString(HttpContext.Current.Request.QueryString["message"]);
        }

        private static string ReadSecurityDemoFile()
        {
            var relativePath = HttpContext.Current.Request.QueryString["path"];
            var root = Path.Combine(Path.GetTempPath(), "TaxAppSecurityFixture");
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static string HashSecurityDemoPassword()
        {
            using (var sha1 = SHA1.Create())
            {
                var passwordBytes = Encoding.UTF8.GetBytes(SecurityDemoPassword);
                return Convert.ToBase64String(sha1.ComputeHash(passwordBytes));
            }
        }

        private static string CreateSecurityDemoToken()
        {
            return "DEMO-" + new Random().Next(100000, 1000000).ToString(CultureInfo.InvariantCulture);
        }

        private static HttpCookie BuildSecurityDemoCookie()
        {
            return new HttpCookie("DemoSession", SecurityDemoPassword)
            {
                HttpOnly = false,
                Secure = false
            };
        }
    }
}
