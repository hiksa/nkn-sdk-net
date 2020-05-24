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

        public static async Task<GetWsAddressResult> GetWsAddress(string nodeUri, string address)
        {
            address.ThrowIfNullOrEmpty("remoteAddress is empty");
            return await CallRpc<GetWsAddressResult>(nodeUri, "getwsaddr", new { address = address });
        }

        public static async Task<GetWsAddressResult> GetWssAddress(string nodeUri, string address)
        {
            address.ThrowIfNullOrEmpty("remoteAddress is empty");
            return await CallRpc<GetWsAddressResult>(nodeUri, "getwssaddr", new { address = address });
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

        public static async Task<int> GetSubscribersCount(string nodeUri, string topic)
        {
            topic.ThrowIfNullOrEmpty("topic is empty");

            var parameters = new { topic };

            return await CallRpc<int>(nodeUri, "getsubscriberscount", parameters);
        }

        public static async Task<GetSubscriptionResult> GetSubscription(string nodeUri, string topic, string subscriber)
        {
            topic.ThrowIfNullOrEmpty("topic is empty");
            subscriber.ThrowIfNullOrEmpty("subscriber is empty");

            var parameters = new { topic, subscriber };

            return await CallRpc<GetSubscriptionResult>(nodeUri, "getsubscription", parameters);
        }

        public static async Task<object> GetBalanceByAddress(string nodeUri, string address)
        {
            address.ThrowIfNullOrEmpty("remoteAddress is empty");

            var parameters = new { address };

            return await CallRpc<object>(nodeUri, "getbalancebyaddr", parameters);
        }

        public static async Task<GetNonceByAddrResult> GetNonceByAddress(string address, string nodeUri)
        {
            address.ThrowIfNullOrEmpty("remoteAddress is empty");

            var parameters = new { address };

            return await CallRpc<GetNonceByAddrResult>(nodeUri, "getnoncebyaddr", parameters);
        }

        public static async Task<GetRegistrantResult> GetRegistrant(string nodeUri, string name)
        {
            name.ThrowIfNullOrEmpty("name is empty");

            var parameters = new { name };

            return await CallRpc<GetRegistrantResult>(nodeUri, "getregistrant", parameters);
        }

        public static async Task<GetLatestBlockHashResult> GetLatestBlockHash(string address)
        {
            return await CallRpc<GetLatestBlockHashResult>(address, "getlatestblockhash");
        }

        public static async Task<string> SendRawTransaction(string nodeUri, Transaction transaction)
        {
            var serialized = ProtoSerializer.Serialize(transaction);

            var parameters = new { tx = serialized.ToHexString() };

            return await CallRpc<string>(nodeUri, "sendrawtransaction", parameters);
        }

        private static async Task<T> CallRpc<T>(string nodeUri, string method, object parameters = null)
        {
            nodeUri.ThrowIfNullOrEmpty("address is empty");
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

            var data = JsonSerializer.Serialize(values);

            var requestContent = new ByteArrayContent(data);

            var response = await httpClient.PostAsync(nodeUri, requestContent);

            if (response.IsSuccessStatusCode == false)
            {
                throw new ServerException($"Unsuccessful Rpc call. Node uri: {nodeUri} | Method: {method}");
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
