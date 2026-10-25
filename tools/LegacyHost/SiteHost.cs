using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web;
using System.Web.Hosting;

namespace LegacyHost
{
    /// <summary>
    /// Lives inside the ASP.NET AppDomain created by ApplicationHost.CreateApplicationHost. Each request is handed to
    /// HttpRuntime through a custom HttpWorkerRequest, which is how Cassini-style servers drive System.Web.
    /// </summary>
    public sealed class SiteHost : MarshalByRefObject
    {
        public SiteHost()
        {
            // This host runs ASP.NET in classic pipeline mode. The anti-forgery helper adds an X-Frame-Options header through
            // Response.Headers, which only exists in IIS integrated mode, so switch just that header off here.
            // Under IIS Express or IIS the app behaves normally. The app itself is not changed.
            var config = Type.GetType(
                "System.Web.Helpers.AntiForgeryConfig, System.Web.WebPages, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
            var property = config == null ? null : config.GetProperty("SuppressXFrameOptionsHeader");
            if (property != null)
            {
                property.SetValue(null, true, null);
            }
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        public HostResponse Process(HostRequest request)
        {
            var worker = new WorkerRequest(request);
            HttpRuntime.ProcessRequest(worker);
            if (!worker.WaitForEnd(TimeSpan.FromSeconds(120)))
            {
                throw new TimeoutException("The request did not finish within 120 seconds.");
            }

            return worker.ToResponse();
        }

        public void Shutdown()
        {
            HostingEnvironment.InitiateShutdown();
        }

        private sealed class WorkerRequest : SimpleWorkerRequest
        {
            private readonly HostRequest _request;
            private readonly string _path;
            private readonly string _query;
            private readonly Dictionary<string, string> _requestHeaders;
            private readonly List<string[]> _responseHeaders = new List<string[]>();
            private readonly MemoryStream _responseBody = new MemoryStream();
            private readonly ManualResetEventSlim _ended = new ManualResetEventSlim(false);
            private int _status = 200;
            private string _statusDescription = "OK";

            public WorkerRequest(HostRequest request)
                : base(string.Empty, string.Empty, null)
            {
                _request = request;

                var separator = request.RawUrl.IndexOf('?');
                var rawPath = separator >= 0 ? request.RawUrl.Substring(0, separator) : request.RawUrl;
                _path = Uri.UnescapeDataString(rawPath);
                _query = separator >= 0 ? request.RawUrl.Substring(separator + 1) : string.Empty;

                _requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in request.Headers ?? new string[0][])
                {
                    string existing;
                    _requestHeaders[pair[0]] = _requestHeaders.TryGetValue(pair[0], out existing)
                        ? existing + ", " + pair[1]
                        : pair[1];
                }
            }

            public bool WaitForEnd(TimeSpan timeout)
            {
                return _ended.Wait(timeout);
            }

            public HostResponse ToResponse()
            {
                return new HostResponse
                {
                    Status = _status,
                    StatusDescription = _statusDescription,
                    Headers = _responseHeaders,
                    Body = _responseBody.ToArray()
                };
            }

            public override string GetUriPath() { return _path; }

            public override string GetQueryString() { return _query; }

            public override string GetRawUrl() { return _request.RawUrl; }

            public override string GetHttpVerbName() { return _request.Method; }

            public override string GetHttpVersion() { return "HTTP/1.1"; }

            public override string GetFilePath() { return _path; }

            public override string GetFilePathTranslated() { return MapPath(_path); }

            public override string MapPath(string virtualPath)
            {
                var root = HttpRuntime.AppDomainAppPath;
                if (string.IsNullOrEmpty(virtualPath) || virtualPath == "/")
                {
                    return root;
                }

                return Path.Combine(root, virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            }

            public override string GetPathInfo() { return string.Empty; }

            public override string GetServerName() { return "localhost"; }

            public override string GetLocalAddress() { return "127.0.0.1"; }

            public override int GetLocalPort() { return _request.Port; }

            public override string GetRemoteAddress() { return "127.0.0.1"; }

            public override int GetRemotePort() { return 0; }

            public override bool IsSecure() { return false; }

            public override string GetServerVariable(string name)
            {
                switch (name)
                {
                    case "SERVER_NAME": return "localhost";
                    case "SERVER_PORT": return _request.Port.ToString();
                    case "SERVER_PORT_SECURE": return "0";
                    case "HTTPS": return "off";
                    case "LOCAL_ADDR": return "127.0.0.1";
                    case "REMOTE_ADDR": return "127.0.0.1";
                    case "REQUEST_METHOD": return _request.Method;
                    case "HTTP_HOST":
                        string host;
                        return _requestHeaders.TryGetValue("Host", out host) ? host : "localhost:" + _request.Port;
                    default: return string.Empty;
                }
            }

            public override string GetKnownRequestHeader(int index)
            {
                string value;
                return _requestHeaders.TryGetValue(GetKnownRequestHeaderName(index), out value) ? value : null;
            }

            public override string GetUnknownRequestHeader(string name)
            {
                string value;
                return _requestHeaders.TryGetValue(name, out value) ? value : null;
            }

            public override string[][] GetUnknownRequestHeaders()
            {
                return _requestHeaders
                    .Where(h => GetKnownRequestHeaderIndex(h.Key) < 0)
                    .Select(h => new[] { h.Key, h.Value })
                    .ToArray();
            }

            public override byte[] GetPreloadedEntityBody()
            {
                return _request.Body != null && _request.Body.Length > 0 ? _request.Body : null;
            }

            public override bool IsEntireEntityBodyIsPreloaded() { return true; }

            public override int GetTotalEntityBodyLength()
            {
                return _request.Body == null ? 0 : _request.Body.Length;
            }

            public override int ReadEntityBody(byte[] buffer, int size) { return 0; }

            public override void SendStatus(int statusCode, string statusDescription)
            {
                _status = statusCode;
                _statusDescription = statusDescription;
            }

            public override void SendKnownResponseHeader(int index, string value)
            {
                _responseHeaders.Add(new[] { GetKnownResponseHeaderName(index), value });
            }

            public override void SendUnknownResponseHeader(string name, string value)
            {
                _responseHeaders.Add(new[] { name, value });
            }

            public override void SendResponseFromMemory(byte[] data, int length)
            {
                _responseBody.Write(data, 0, length);
            }

            public override void SendResponseFromMemory(IntPtr data, int length)
            {
                var buffer = new byte[length];
                Marshal.Copy(data, buffer, 0, length);
                _responseBody.Write(buffer, 0, length);
            }

            public override void SendResponseFromFile(string filename, long offset, long length)
            {
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    CopyRange(stream, offset, length);
                }
            }

            public override void SendResponseFromFile(IntPtr handle, long offset, long length)
            {
                using (var stream = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(handle, false), FileAccess.Read))
                {
                    CopyRange(stream, offset, length);
                }
            }

            public override void FlushResponse(bool finalFlush) { }

            public override void EndOfRequest()
            {
                _ended.Set();
            }

            private void CopyRange(Stream stream, long offset, long length)
            {
                var count = length < 0 ? stream.Length - offset : length;
                stream.Seek(offset, SeekOrigin.Begin);
                var buffer = new byte[Math.Min(81920, Math.Max(1, count))];
                while (count > 0)
                {
                    var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
                    if (read <= 0)
                    {
                        break;
                    }

                    _responseBody.Write(buffer, 0, read);
                    count -= read;
                }
            }
        }
    }
}
