using System;
using System.Collections.Generic;

namespace LegacyHost
{
    /// <summary>Request data passed from the HttpListener side into the ASP.NET AppDomain.</summary>
    [Serializable]
    public sealed class HostRequest
    {
        public string Method;
        public string RawUrl;
        public int Port;
        public string[][] Headers;
        public byte[] Body;
    }

    /// <summary>Response data passed back out of the ASP.NET AppDomain.</summary>
    [Serializable]
    public sealed class HostResponse
    {
        public int Status;
        public string StatusDescription;
        public List<string[]> Headers;
        public byte[] Body;
    }
}
