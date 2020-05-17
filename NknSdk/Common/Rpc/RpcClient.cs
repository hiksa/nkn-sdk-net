using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using Utf8Json;
using Utf8Json.Resolvers;

using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Transaction;
using NknSdk.Common.Rpc.Results;

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
            return await CallRpc<GetWsAddressResult>(address, "getwsaddr", new { address = remoteAddress });
        }

        public static async Task<GetWsAddressResult> GetWssAddress(string address, string remoteAddress)
        {
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
            var parameters = new { topic };

            return await CallRpc<int>(address, "getsubscriberscount", parameters);
        }

        public static async Task<GetSubscriptionResult> GetSubscription(string address, string topic, string subscriber)
        {
            var parameters = new { topic, subscriber };

            return await CallRpc<GetSubscriptionResult>(address, "getsubscription", parameters);
        }

        public static async Task<object> GetBalanceByAddress(string address, string remoteAddress)
        {
            var parameters = new { address = remoteAddress };

            return await CallRpc<object>(address, "getbalancebyaddr", parameters);
        }

        public static async Task<GetNonceByAddrResult> GetNonceByAddress(string address, string remoteAddress)
        {
            var parameters = new { address = remoteAddress };

            return await CallRpc<GetNonceByAddrResult>(address, "getnoncebyaddr", parameters);
        }

        public static async Task<GetRegistrantResult> GetRegistrant(string address, string name)
        {
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
                throw new HttpRequestException($"Unsuccessful Rpc call. Node address: {address} | Method: {method}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                throw new HttpRequestException("");
            }

            var rpcResponse = JsonSerializer.Deserialize<RpcResponse<T>>(responseContent, StandardResolver.CamelCase);

            if (rpcResponse.IsSuccess == false)
            {
                throw new HttpRequestException(rpcResponse.Error.Data);
            }

            return rpcResponse.Result;
        }
    }
}
