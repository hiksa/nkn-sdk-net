using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Ncp;

using NknSdk.Client;
using NknSdk.Wallet;

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

            var wallet = new Wallet(new WalletOptions { SeedHex = seed1, Version = 1 });
            var walletJson = wallet.ToJson();

            var test = Wallet.FromJson(walletJson, new WalletOptions());

            var options = new MultiClientOptions();
            options.NumberOfSubClients = 4;
            options.ResponseTimeout = 500_000;
            options.Seed = seed1;

            var client = new MultiClient(options);

            var connected = false;

            client.OnConnect(async (request) => 
            {
                Console.WriteLine(request.Address);

                var response = await client.SendToManyAsync(new List<string> { address2, address3 }, new byte[] { 42 });

                Console.WriteLine(string.Join(",", response));
            });

            client.OnMessage(request =>
            {
                Console.WriteLine("***On Message***");
                Console.WriteLine("Remote address: " + request.Source);
                Console.WriteLine("Data type: " + request.PayloadType.ToString());
                Console.WriteLine("Data: " + string.Join(", ", request.Payload));
                return Task.FromResult((object)new byte[] { 1, 2, 3, 4, 5, 6, 7 });
            });

            Session sess = null;
            client.OnSession(x =>
            {
                Console.WriteLine("***On Session***");
                sess = x;

                return Task.FromResult((object)true);
            });

            Console.ReadKey();
        }

        private static async Task DialAndSendFile(string address3, MultiClient client)
        {
            var session = await client.DialAsync(address3, new SessionConfiguration());
            Console.WriteLine("Session established");

            session.SetLinger(-1);

            var filePath = @"..\..\..\extra.exe";
            //var filePath = @"..\..\..\large.txt";
            //var filePath = @"..\..\..\med.pdf";
            //var filePath = @"..\..\..\small.txt";
            var file = new FileInfo(filePath);
            var fileNameEncoded = Encoding.ASCII.GetBytes(file.Name);

            Console.WriteLine("Writing file name length " + fileNameEncoded.Length);
            await WriteUintToSession(session, (uint)fileNameEncoded.Length);
            await Task.Delay(200);
            Console.WriteLine("Writing file name " + fileNameEncoded);
            await session.WriteAsync(fileNameEncoded);
            await Task.Delay(200);
            Console.WriteLine("Writing file length " + file.Length);
            await WriteUintToSession(session, (uint)file.Length);
            await Task.Delay(200);

            var sw = Stopwatch.StartNew();
            var fileBytes = File.ReadAllBytes(filePath);
            await session.WriteAsync(fileBytes);
            sw.Stop();

            var kilobytesPerSecond = file.Length / sw.ElapsedMilliseconds;

            Console.WriteLine($"Finished writing file {file.Name} in {sw.Elapsed} with {kilobytesPerSecond} kbps");

            EnsureCorrectSend(session, fileBytes);

            Console.WriteLine("***Finito***");
            Console.WriteLine("***Finito***");
            Console.WriteLine("***Finito***");
        }

        private static void EnsureCorrectSend(Session session, byte[] fileBytes)
        {
            //try
            //{
            //    var queued = session.AllQueued;
            //    var offset = queued.Count - fileBytes.Length;
            //    for (int i = 0; i < fileBytes.Length; i++)
            //    {
            //        if (fileBytes[i] != queued[i + offset])
            //        {

            //        }
            //    }

            //    var sent = session.AllSent;
            //    for (int i = 0; i < sent.Count; i++)
            //    {
            //        if (queued[i] != sent[i])
            //        {

            //        }
            //    }
            //}
            //catch (Exception e)
            //{

            //}

            //for (int i = 0; i < sent.Count; i++)
            //{
            //    if (fileBytes[i] != sent[i + offset])
            //    {

            //    }
            //}
        }

        static async Task WriteUintToSession(Session session, uint value)
        {
            var data = BitConverter.GetBytes(value);
            await session.WriteAsync(data);
        }
    }
}
