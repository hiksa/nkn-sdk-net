using Ncp;
using NknSdk.Client;
using NknSdk.Client.Model;
using NknSdk.Common;
using System;
using System.Threading.Tasks;

namespace Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            var seed1 = "b71a349865ed5907821e903389e83424d037d4e1680935fd3c1f33408df2fdf5";
            var address1 = "3fc6bbdae9be658e7b7e85de65d04f3bd39ad41afc316b82314cca7c62e9fd6e";
            var seed2 = "f40bc0903ae5064ec225a82d1ca8ca6c63d4ecf2c41689b82fc6bf581ea3b67b";
            var address2 = "fc6c231915d568f50e4dac2858e833ec7fc6c27e9cd040ced08f941e5c8a32f7";
            var seed3 = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var address3 = "a4a5d152f83fe8802ba1329ed3e31aa2f3492fc1a775ee02f41927103f0cc088";
            var seed4 = "16735a849deaa136ba6030c3695c4cbdc9b275d5d9a9f46b836841ab4a36179e";
            var address4 = "7ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9";

            var options = MultiClientOptions.Default;
            options.NumberOfSubClients = 1;
            options.ResponseTimeout = 500_000;
            options.Seed = seed1;
            //options.Identifier = "55";

            var sessionConfig = SessionConfiguration.Default;

            var client = new MultiClient(options);
            var connected = false;
            client.Connected += (object sender, EventArgs args) =>
            {
                Console.WriteLine("***Connected***");
                if (connected)
                {
                    return;
                }

                client.Listen(new string[] { address2 });

                Task.Run(async delegate
                {
                    
                });
                //client
                //    .Send(
                //        address2,
                //        new byte[] { 1, 2, 3 },
                //        new SendOptions { IsEncrypted = true })
                //    .GetAwaiter()
                //    .GetResult();

              //  var session = client.Dial(address2, SessionConfiguration.Default).GetAwaiter().GetResult();

                //   Console.WriteLine(session.Config.CheckBytesReadInterval);

                var sendOptions = new SendOptions
                {
                    IsEncrypted = true
                };

                //var task = client
                //    .Send(
                //        address4,
                //        //   "75",
                //        new byte[] { 1, 2, 3, 4, 5, 100 },
                //        sendOptions)
                //    .GetAwaiter()
                //    .GetResult();

                //task.GetAwaiter().GetResult();
            };

            client.OnMessage(request =>
            {
                Console.WriteLine("***On Message***");
                Console.WriteLine("Remote address: " + request.Source);
                Console.WriteLine("Data type: " + request.PayloadType.ToString());
                Console.WriteLine("Data: " + string.Join(", ", request.Payload));
                return Task.FromResult((object)new byte[] { 1, 2 });
            });


            Session sess = null;
            client.OnSession(x =>
            {
                Console.WriteLine("Session");
                Console.WriteLine(x.Config.CheckBytesReadInterval);
                sess = x;

                Task.Factory.StartNew(async delegate
                {
                    //var c = 0;
                    //while (true)
                    //{
                    //    var payload = new byte[] { (byte)c, (byte)c, (byte)c };
                    //    Console.WriteLine("Session writing... " + string.Join(", ", payload));
                    //    await sess.WriteAsync(payload);

                    //    c++;

                    //    await Task.Delay(1000);
                    //}


                    var c = 0;
                    while (true)
                    {
                        if (sess != null)
                        {
                            try
                            {
                                var test = await sess.ReadAsync();
                                Console.WriteLine("Session read " + c);
                                c++;
                                Console.WriteLine(string.Join(", ", test));
                            }
                            catch (Exception e)
                            {

                            }
                        }
                    }
                });

                return Task.FromResult((object)true);
            });

            Task.Run(async () =>
            {


                //var task = client
                //    .Send(
                //        address4,
                //        //"75",
                //        new byte[] { 1, 1 },
                //        sendOptions,
                //        x =>
                //        {
                //            var type = x.GetType();
                //            if (type == typeof(string))
                //            {
                //                Console.WriteLine(x?.ToString());
                //            }
                //            else if (type == typeof(byte[]))
                //            {
                //                Console.WriteLine(string.Join(", ", (byte[])x));
                //            }
                //        })
                //    .GetAwaiter()
                //    .GetResult();

            }).GetAwaiter().GetResult();

            client.DataReceived += Client_DataReceived;

            client.TextReceived += Client_TextReceived;

            Console.ReadKey();
        }

        private static void Client_TextReceived(string sender, string text)
        {
            Console.WriteLine($"Text messege received from: {sender}");
            Console.WriteLine($"Text contents: {text}");
        }

        private static void Client_DataReceived(string sender, byte[] data)
        {
            Console.WriteLine("Data received. Length: " + data.Length);
            Console.WriteLine("[ " + string.Join(", ", data) + " ]");
        }
    }
}
