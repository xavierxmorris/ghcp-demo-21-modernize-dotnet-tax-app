using System;
using System.IO;
using System.Net;
using System.Threading;

namespace LegacyHost
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string site = null;
            var port = 8081;

            try
            {
                for (var i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--site":
                            site = args[++i];
                            break;
                        case "--port":
                            port = int.Parse(args[++i]);
                            break;
                        case "--help":
                        case "-h":
                            PrintUsage();
                            return 0;
                        default:
                            Console.Error.WriteLine("Unknown argument: " + args[i]);
                            PrintUsage();
                            return 64;
                    }
                }
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException || ex is FormatException)
            {
                Console.Error.WriteLine("Bad arguments.");
                PrintUsage();
                return 64;
            }

            if (string.IsNullOrEmpty(site))
            {
                PrintUsage();
                return 64;
            }

            try
            {
                using (var server = new SiteServer(Path.GetFullPath(site), port))
                {
                    server.Start();
                    Console.WriteLine("Tax App (legacy, .NET Framework) listening on http://localhost:" + port + "/");
                    Console.WriteLine("Site folder: " + Path.GetFullPath(site));
                    Console.WriteLine("Press Ctrl+C to stop.");

                    var stop = new ManualResetEvent(false);
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        stop.Set();
                    };
                    stop.WaitOne();
                }

                return 0;
            }
            catch (HttpListenerException ex)
            {
                Console.Error.WriteLine("Could not listen on port " + port + ": " + ex.Message);
                return 2;
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 3;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: TaxApp.LegacyHost --site <path to legacy\\TaxApp.Web> [--port 8081]");
            Console.WriteLine("Runs the legacy ASP.NET MVC 5 site in-process, without IIS Express.");
        }
    }
}
