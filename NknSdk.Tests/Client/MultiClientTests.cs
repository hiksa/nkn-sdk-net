using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using NknSdk.Client;
using NknSdk.Common.Options;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace NknSdk.Tests.Client
{
    [CollectionDefinition("MultiClientTests Collection", DisableParallelization = true)]
    [Collection("MultiClientTests Collection")]
    public class MultiClientTests
    {
        private const int TestTimeout = 40_000;
        private const int PauseBetweenTests = 2_000;

        [Fact(Timeout = TestTimeout)]
        public async Task ShouldSendAndReceiveSessionMessagesCorrectly()
        {
            var seed1 = "d0de404077ede0fdd1dfd15ab2934018fa2f8d1ac1effb4af577dbedc897b0b8";
            var seed2 = "16735a849deaa136ba6030c3695c4cbdc9b275d5d9a9f46b836841ab4a36179e";

            var address1 = "a4a5d152f83fe8802ba1329ed3e31aa2f3492fc1a775ee02f41927103f0cc088";
            var address2 = "7ade8659d490283303beb2f224cff1f3709364ce6765a7132d65ed1a6e10ecf9";

            var data = new List<byte[]>
            {
                new byte[] { 1, 1, 1 },
                new byte[] { 2, 2, 2 },
                new byte[] { 3, 3, 3 },
                new byte[] { 4, 4, 4 },
                new byte[] { 5, 5, 5 },
            };

            var dataCount = data.Count;

            MultiClient client1 = default;
            var client2 = new MultiClient(new MultiClientOptions { SeedHex = seed2 });
            
            client2.Listen(new string[] { address1 });

            var finished = false;

            client2.OnSession(session =>
            {
                Task.Delay(500).GetAwaiter().GetResult();

                Assert.Equal(address1, session.RemoteAddress);

                var dataSize = data.Select(x => x.Length).FirstOrDefault();

                for (int i = 0; i < dataCount; i++)
                {
                    var received = session.ReadAsync(dataSize).GetAwaiter().GetResult();

                    Assert.Equal(data[i], received);
                }

                finished = true;

                return Task.FromResult((object)true);
            });

            client2.OnConnect(async request =>
            {
                await Task.Delay(500);

                client1 = new MultiClient(new MultiClientOptions { SeedHex = seed1 });

                client1.OnConnect(async request =>
                {
                    var session = await client1.DialAsync(address2);

                    Assert.Equal(address2, session.RemoteAddress);

                    for (int i = 0; i < dataCount; i++)
                    {
                        await session.WriteAsync(data[i]);
                    }
                });

                await Task.Yield();
            });

            while (finished == false)
            {
                await Task.Delay(100);
            }

            await Task.Delay(1_000);

            var closeTasks = new List<Task>
            {
                client1.CloseAsync(),
                client2.CloseAsync()
            };

            await Task.WhenAll(closeTasks);

            await Task.Delay(PauseBetweenTests);

            Assert.True(finished);
        }

        [Fact(Timeout = TestTimeout)]
        public async Task ShouldSendAndReceiveBinaryDataCorrectly()
        {
            var seed1 = "b71a349865ed5907821e903389e83424d037d4e1680935fd3c1f33408df2fdf5";
            var seed2 = "f40bc0903ae5064ec225a82d1ca8ca6c63d4ecf2c41689b82fc6bf581ea3b67b";

            var address1 = "3fc6bbdae9be658e7b7e85de65d04f3bd39ad41afc316b82314cca7c62e9fd6e";
            var address2 = "fc6c231915d568f50e4dac2858e833ec7fc6c27e9cd040ced08f941e5c8a32f7";

            MultiClient client1 = default;
            var client2 = new MultiClient(new MultiClientOptions { SeedHex = seed2 });

            var expectedRequestData = new byte[] { 5, 5, 5, 5 };
            var expectedResponseData = new byte[] { 1, 2, 3, 4, 5 };

            client2.OnMessage(request =>
            {
                Assert.Equal(address1, request.Source);
                Assert.Equal(expectedRequestData, request.Payload);

                return Task.FromResult((object)expectedResponseData);
            });

            var finished = false;

            client2.OnConnect(request =>
            {
                Task.Delay(500).GetAwaiter().GetResult();

                client1 = new MultiClient(new MultiClientOptions { SeedHex = seed1 });

                client1.OnConnect(async request =>
                {
                    var response = await client1.SendAsync<byte[]>(address2, expectedRequestData);

                    Assert.NotNull(response.Result);
                    Assert.Equal(expectedResponseData, response.Result);

                    finished = true;
                });
            });

            while (finished == false)
            {
                await Task.Delay(100);
            }

            var closeTasks = new List<Task>
            {
                client1.CloseAsync(),
                client2.CloseAsync()
            };

            await Task.Delay(100);

            await Task.WhenAll(closeTasks);

            await Task.Delay(PauseBetweenTests);

            Assert.True(finished);
        }

        [Fact(Timeout = TestTimeout)]
        public async Task ShouldSendAndReceiveTextMessageCorrectly()
        {
            var seed1 = "b71a349865ed5907821e903389e83424d037d4e1680935fd3c1f33408df2fdf5";
            var seed2 = "f40bc0903ae5064ec225a82d1ca8ca6c63d4ecf2c41689b82fc6bf581ea3b67b";

            var address1 = "3fc6bbdae9be658e7b7e85de65d04f3bd39ad41afc316b82314cca7c62e9fd6e";
            var address2 = "fc6c231915d568f50e4dac2858e833ec7fc6c27e9cd040ced08f941e5c8a32f7";

            MultiClient client1 = default;
            var client2 = new MultiClient(new MultiClientOptions { SeedHex = seed2 });

            var expectedRequestText = "Hello world!";
            var expectedResponseText = "Hi there!";

            client2.OnMessage(async request =>
            {
                Assert.Equal(address1, request.Source);
                Assert.Equal(expectedRequestText, request.TextMessage);

                return await Task.FromResult(expectedResponseText);
            });

            var finished = false;

            client2.OnConnect(request =>
            {
                client1 = new MultiClient(new MultiClientOptions { SeedHex = seed1 });

                client1.OnConnect(async request =>
                {
                    var response = await client1.SendAsync<string>(address2, expectedRequestText);

                    Assert.NotNull(response.Result);
                    Assert.Equal(expectedResponseText, response.Result);

                    finished = true;
                });
            });

            while (finished == false)
            {
                await Task.Delay(100);
            }

            var closeTasks = new List<Task>
            {
                client1.CloseAsync(),
                client2.CloseAsync()
            };

            await Task.Delay(1_000);

            await Task.WhenAll(closeTasks);

            await Task.Delay(PauseBetweenTests);

            Assert.True(finished);
        }
    }
}
