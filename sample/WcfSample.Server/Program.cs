namespace WcfSample
{
    using System;
    using System.ServiceModel;

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(@"Preparing the database...");
            MyRemoteService.RecreateDatabase();

            // Start self-hosted WCF server
            using (var host = new ServiceHost(new MyRemoteService(), new Uri(WcfShared.UriString)))
            { 
                host.AddDefaultEndpoints();
                host.Open();
                foreach (Uri addr in host.BaseAddresses)
                {
                    Console.WriteLine($@"The service is ready at {addr}");
                }

                Console.WriteLine(@"Press any key to stop.");
                Console.ReadKey();
                host.Close();
            }
        }
    }
}
