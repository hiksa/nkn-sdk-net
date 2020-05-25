using Ncp;
using NknSdk.Client;
using NknSdk.Client.Model;
using NknSdk.Common;
using System;
using System.IO;
using System.Text;
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

            var sessionConfig = SessionConfiguration.Default;

            var client = new MultiClient(options);
            var connected = false;
            client.Connected += async (object sender, EventArgs args) =>
            {
                Console.WriteLine("***Connected***");
                if (connected)
                {
                    return;
                }

                connected = true;

                await Task.Delay(1000);

                client.Listen(new string[] { address2 });

                var session = await client.DialAsync(address4, SessionConfiguration.Default);
                Console.WriteLine("Session established");

                session.SetLinger(-1);

                //var filePath = @"..\..\..\large.txt";
                var filePath = @"..\..\..\small.txt";
                var file = new FileInfo(filePath);
                var fileNameEncoded = Encoding.ASCII.GetBytes(file.Name);

                Console.WriteLine("Writing file name length " + fileNameEncoded.Length);
                await WriteUintToSession(session, (uint)fileNameEncoded.Length);
                //await Task.Delay(1000);
                Console.WriteLine("Writing file name " + fileNameEncoded);
                await session.WriteAsync(fileNameEncoded);
                //await Task.Delay(1000);
                Console.WriteLine("Writing file length " + file.Length);
                await WriteUintToSession(session, (uint)file.Length);
                //await Task.Delay(1000);

                var fileBytes = File.ReadAllBytes(filePath);
                await session.WriteAsync(fileBytes);

                Console.WriteLine("Finished writing file " + file.Name);

                //    Task.Factory.StartNew(async delegate
                //    {
                //        var c = 0;

                //        while (true)
                //        {
                //            var payload = new byte[] { (byte)c, (byte)c, (byte)c };
                //            Console.WriteLine($"Session writing... {c} ..." + string.Join(", ", payload));
                //            await session.WriteAsync(payload);

                //            c++;

                //            await Task.Delay(2000);
                //        }

                //        //while (true)
                //        //{
                //        //    try
                //        //    {
                //        //        var test = await session.ReadAsync();
                //        //        Console.WriteLine("Session read " + c);
                //        //        c++;
                //        //        Console.WriteLine(string.Join(", ", test));
                //        //    }
                //        //    catch (Exception e)
                //        //    {
                //        //        Console.WriteLine(e.Message);
                //        //    }
                //        //}
                //    });
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
                Console.WriteLine("***On Session***");
                sess = x;




                //Task.Factory.StartNew(async delegate
                //{
                //    var c = 0;
                //    while (true)
                //    {
                //        var payload = new byte[] { (byte)c, (byte)c, (byte)c };
                //        Console.WriteLine("Session writing... " + string.Join(", ", payload));
                //        await sess.WriteAsync(payload);

                //        c++;

                //        await Task.Delay(1000);
                //    }


                //    //var c = 0;
                //    //while (true)
                //    //{
                //    //    if (sess != null)
                //    //    {
                //    //        try
                //    //        {
                //    //            var test = await sess.ReadAsync();
                //    //            Console.WriteLine("Session read " + c);
                //    //            c++;
                //    //            Console.WriteLine(string.Join(", ", test));
                //    //        }
                //    //        catch (Exception e)
                //    //        {

                //    //        }
                //    //    }
                //    //}
                //});

                return Task.FromResult((object)true);
            });
    
            client.DataReceived += Client_DataReceived;

            client.TextReceived += Client_TextReceived;

            Console.ReadKey();
        }

        static void Client_TextReceived(string sender, string text)
        {
            Console.WriteLine($"Text messege received from: {sender}");
            Console.WriteLine($"Text contents: {text}");
        }

        static void Client_DataReceived(string sender, byte[] data)
        {
            Console.WriteLine("Data received. Length: " + data.Length);
            Console.WriteLine("[ " + string.Join(", ", data) + " ]");
        }

        static async Task WriteUintToSession(Session session, uint value)
        {
            var data = BitConverter.GetBytes(value);
            await session.WriteAsync(data);
        }
    }
}
