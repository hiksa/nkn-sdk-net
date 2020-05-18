using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using Utf8Json;
using Utf8Json.Resolvers;

using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Transaction;
using NknSdk.Common.Rpc.Results;
using NknSdk.Common.Exceptions;

namespace NknSdk.Common.Rpc
{
    public class RpcClient
    {
        private static HttpClient httpClient;

        static RpcClient()
        {
            httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };
        }

        public static async Task<GetWsAddressResult> GetWsAddress(string address, string remoteAddress)
        {
            remoteAddress.ThrowIfNullOrEmpty("remoteAddress is empty");
            return await CallRpc<GetWsAddressResult>(address, "getwsaddr", new { address = remoteAddress });
        }

        public static async Task<GetWsAddressResult> GetWssAddress(string address, string remoteAddress)
        {
            remoteAddress.ThrowIfNullOrEmpty("remoteAddress is empty");
            return await CallRpc<GetWsAddressResult>(address, "getwssaddr", new { address = remoteAddress });
        }

        public static async Task<GetSubscribersResult> GetSubscribers(
            string address,
            string topic,
            int offset = 0,
            int limit = 1000,
            bool meta = false,
            bool txPool = false)
        {
            topic.ThrowIfNullOrEmpty("topic is empty");

            var parameters = new
            {
                topic,
                offset,
                limit,
                meta,
                txPool
            };

            return await CallRpc<GetSubscribersResult>(address, "getsubscribers", parameters);
        }

        public static async Task<int> GetSubscribersCount(string address, string topic)
        {
            topic.ThrowIfNullOrEmpty("topic is empty");

            var parameters = new { topic };

            return await CallRpc<int>(address, "getsubscriberscount", parameters);
        }

        public static async Task<GetSubscriptionResult> GetSubscription(string address, string topic, string subscriber)
        {
            topic.ThrowIfNullOrEmpty("topic is empty");
            subscriber.ThrowIfNullOrEmpty("subscriber is empty");

            var parameters = new { topic, subscriber };

            return await CallRpc<GetSubscriptionResult>(address, "getsubscription", parameters);
        }

        public static async Task<object> GetBalanceByAddress(string address, string remoteAddress)
        {
            remoteAddress.ThrowIfNullOrEmpty("remoteAddress is empty");

            var parameters = new { address = remoteAddress };

            return await CallRpc<object>(address, "getbalancebyaddr", parameters);
        }

        public static async Task<GetNonceByAddrResult> GetNonceByAddress(string address, string remoteAddress)
        {
            remoteAddress.ThrowIfNullOrEmpty("remoteAddress is empty");

            var parameters = new { address = remoteAddress };

            return await CallRpc<GetNonceByAddrResult>(address, "getnoncebyaddr", parameters);
        }

        public static async Task<GetRegistrantResult> GetRegistrant(string address, string name)
        {
            name.ThrowIfNullOrEmpty("name is empty");

            var parameters = new { name };

            return await CallRpc<GetRegistrantResult>(address, "getregistrant", parameters);
        }

        public static async Task<GetLatestBlockHashResult> GetLatestBlockHash(string address)
        {
            return await CallRpc<GetLatestBlockHashResult>(address, "getlatestblockhash");
        }

        public static async Task<string> SendRawTransaction(string address, Transaction transaction)
        {
            var serialized = ProtoSerializer.Serialize(transaction);

            var parameters = new { tx = serialized.ToHexString() };

            return await CallRpc<string>(address, "sendrawtransaction", parameters);
        }

        private static async Task<T> CallRpc<T>(string address, string method, object parameters = null)
        {
            address.ThrowIfNullOrEmpty("address is empty");
            method.ThrowIfNullOrEmpty("method is empty");

            if (parameters == null)
            {
                parameters = new { };
            }

            var values = new Dictionary<string, object>
            {
                { "id", "nkn-sdk-js" },
                { "jsonrpc", 2.0 },
                { "method", method },
                { "params", parameters }
            };

            var serialized = JsonSerializer.Serialize(values);

            var requestContent = new ByteArrayContent(serialized);

            var response = await httpClient.PostAsync(address, requestContent);

            if (response.IsSuccessStatusCode == false)
            {
                throw new ServerException($"Unsuccessful Rpc call. Node address: {address} | Method: {method}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                throw new ServerException("rpc response is empty");
            }

            var rpcResponse = JsonSerializer.Deserialize<RpcResponse<T>>(responseContent, StandardResolver.CamelCase);

            if (rpcResponse.IsSuccess == false)
            {
                throw new ServerException(rpcResponse.Error.Data);
            }

            if (rpcResponse.Result != null)
            {
                return rpcResponse.Result;
            }

            throw new InvalidResponseException("rpc response contains no result or error field");
        }
    }
}
