using Sentry;

namespace CL_CLegendary_Launcher_.Class
{
    public static class CrashReportManager
    {
        public static void Enable()
        {
            if (SentrySdk.IsEnabled) return;

            SentrySdk.Init(o =>
            {
                o.Dsn = $"{Secrets.DNS_KEY_AutoReport}";
                o.Debug = false;
                o.SendDefaultPii = false;

                o.SetBeforeSend(sentryEvent =>
                {
                    if (sentryEvent.Exception != null)
                    {
                        string errorString = sentryEvent.Exception.ToString();
                        if (errorString.Contains("System.Net.Sockets.SocketException") ||
                            errorString.Contains("A connection attempt failed"))
                        {
                            return null;
                        }
                    }

                    sentryEvent.User = new SentryUser
                    {
                        Id = "Anonymous",
                        IpAddress = "0.0.0.0"
                    };

                    var keepOs = sentryEvent.Contexts.OperatingSystem;
                    var keepRuntime = sentryEvent.Contexts.Runtime;
                    var keepApp = sentryEvent.Contexts.App;

                    sentryEvent.Contexts.Clear();

                    if (keepOs != null) sentryEvent.Contexts["os"] = keepOs;
                    if (keepRuntime != null) sentryEvent.Contexts["runtime"] = keepRuntime;
                    if (keepApp != null) sentryEvent.Contexts["app"] = keepApp;

                    return sentryEvent;
                });
            });
        }
        public static void Disable()
        {
            if (SentrySdk.IsEnabled)
            {
                SentrySdk.Close();
            }
        }
    }
}