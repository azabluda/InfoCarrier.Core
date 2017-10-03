namespace WcfSample
{
    using System;

    public static class WcfShared
    {
        public static string UriString =>
            Environment.GetEnvironmentVariable(@"Wcf__DefaultUri")
            ?? @"http://localhost:8080/MyRemoteService";
    }
}