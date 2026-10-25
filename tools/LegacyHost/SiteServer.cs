using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web.Hosting;

namespace LegacyHost
{
    /// <summary>Accepts HTTP requests with HttpListener and forwards them to the ASP.NET AppDomain.</summary>
    internal sealed class SiteServer : IDisposable
    {
        private static readonly string[] SkippedResponseHeaders = { "Content-Length", "Transfer-Encoding", "Connection", "Keep-Alive" };

        // IIS request filtering hides these segments (HTTP 404.8). Hosting System.Web without IIS has to do the same,
        // otherwise the deductions file under App_Data and the assemblies under bin would be downloadable.
        private static readonly string[] HiddenSegments =
        {
            "bin", "App_Code", "App_Data", "App_GlobalResources", "App_LocalResources", "App_WebReferences", "App_Browsers"
        };

        private readonly string _sitePath;
        private readonly int _port;
        private HttpListener _listener;
        private SiteHost _host;
        private volatile bool _stopping;

        public SiteServer(string sitePath, int port)
        {
            _sitePath = sitePath;
            _port = port;
        }

        public void Start()
        {
            if (!File.Exists(Path.Combine(_sitePath, "Web.config")))
            {
                throw new FileNotFoundException("No Web.config in the site folder: " + _sitePath);
            }

            if (!File.Exists(Path.Combine(_sitePath, "bin", "TaxApp.Web.dll")))
            {
                throw new FileNotFoundException("The site has not been built yet. Run: dotnet build legacy\\TaxApp.sln");
            }

            PlaceHostAssemblyInSiteBin();
            _host = (SiteHost)ApplicationHost.CreateApplicationHost(typeof(SiteHost), "/", _sitePath);

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:" + _port + "/");
            _listener.Start();
            _listener.BeginGetContext(OnContext, null);
        }

        public void Dispose()
        {
            _stopping = true;
            try
            {
                if (_listener != null)
                {
                    _listener.Close();
                }

                if (_host != null)
                {
                    _host.Shutdown();
                }
            }
            catch (Exception)
            {
                // Shutting down: nothing useful to do with a failure here.
            }
        }

        // ASP.NET loads SiteHost from <site>\bin, so a copy of this assembly has to be there.
        private void PlaceHostAssemblyInSiteBin()
        {
            var source = Assembly.GetExecutingAssembly().Location;
            var target = Path.Combine(_sitePath, "bin", Path.GetFileName(source));
            try
            {
                File.Copy(source, target, true);
            }
            catch (IOException)
            {
                if (!File.Exists(target))
                {
                    throw;
                }
            }
        }

        private void OnContext(IAsyncResult result)
        {
            HttpListenerContext context;
            try
            {
                context = _listener.EndGetContext(result);
            }
            catch (Exception)
            {
                return;
            }

            if (!_stopping)
            {
                try
                {
                    _listener.BeginGetContext(OnContext, null);
                }
                catch (Exception)
                {
                    return;
                }
            }

            Handle(context);
        }

        private void Handle(HttpListenerContext context)
        {
            try
            {
                if (IsHiddenPath(context.Request.RawUrl))
                {
                    var hidden = System.Text.Encoding.UTF8.GetBytes("404.8 Hidden namespace: this folder is never served, as under IIS.");
                    context.Response.StatusCode = 404;
                    context.Response.ContentType = "text/plain";
                    context.Response.ContentLength64 = hidden.Length;
                    context.Response.OutputStream.Write(hidden, 0, hidden.Length);
                    return;
                }

                var response = _host.Process(ToHostRequest(context.Request));
                Write(response, context.Response, context.Request.HttpMethod);
            }
            catch (Exception ex)
            {
                try
                {
                    var message = System.Text.Encoding.UTF8.GetBytes("Host error: " + ex.Message);
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "text/plain";
                    context.Response.ContentLength64 = message.Length;
                    context.Response.OutputStream.Write(message, 0, message.Length);
                }
                catch (Exception)
                {
                    // The client has gone away.
                }
            }
            finally
            {
                try
                {
                    context.Response.Close();
                }
                catch (Exception)
                {
                    // The client has gone away.
                }
            }
        }

        private static bool IsHiddenPath(string rawUrl)
        {
            var queryStart = rawUrl.IndexOf('?');
            var path = Uri.UnescapeDataString(queryStart >= 0 ? rawUrl.Substring(0, queryStart) : rawUrl).Replace('\\', '/');
            foreach (var segment in path.Split('/'))
            {
                var name = segment.TrimEnd('.', ' ');
                if (Array.Exists(HiddenSegments, hidden => string.Equals(hidden, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private HostRequest ToHostRequest(HttpListenerRequest request)
        {
            var headers = new System.Collections.Generic.List<string[]>();
            foreach (var name in request.Headers.AllKeys)
            {
                foreach (var value in request.Headers.GetValues(name) ?? new string[0])
                {
                    headers.Add(new[] { name, value });
                }
            }

            byte[] body = null;
            if (request.HasEntityBody)
            {
                using (var buffer = new MemoryStream())
                {
                    request.InputStream.CopyTo(buffer);
                    body = buffer.ToArray();
                }
            }

            return new HostRequest
            {
                Method = request.HttpMethod,
                RawUrl = request.RawUrl,
                Port = _port,
                Headers = headers.ToArray(),
                Body = body
            };
        }

        private static void Write(HostResponse source, HttpListenerResponse target, string method)
        {
            target.StatusCode = source.Status;
            target.StatusDescription = source.StatusDescription;

            foreach (var header in source.Headers)
            {
                var name = header[0];
                var value = header[1];

                if (Array.Exists(SkippedResponseHeaders, s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    target.ContentType = value;
                }
                else if (string.Equals(name, "Location", StringComparison.OrdinalIgnoreCase))
                {
                    target.RedirectLocation = value;
                }
                else
                {
                    try
                    {
                        target.AppendHeader(name, value);
                    }
                    catch (ArgumentException)
                    {
                        // A header the listener manages itself.
                    }
                }
            }

            target.ContentLength64 = source.Body.Length;
            if (!string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                target.OutputStream.Write(source.Body, 0, source.Body.Length);
            }
        }
    }
}
