﻿using System;
using System.Text;
using StreamChat.Rest;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace StreamChat
{
    public class Client
    {
        internal const string BaseUrlFormat = "https://chat-{0}.stream-io-api.com";
        internal const string BaseUrlPath = "/api/v1.0/";
        internal static readonly Dictionary<ApiLocation, string> Locations = new Dictionary<ApiLocation, string>()
        {
            {ApiLocation.USEast, "us-east-1"}
        };

        internal static readonly object JWTHeader = new
        {
            typ = "JWT",
            alg = "HS256"
        };

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        readonly ClientOptions _options;
        readonly RestClient _client;
        readonly string _apiSecret;
        readonly string _apiKey;
        readonly string _token;

        public Client(string apiKey, string apiSecret, ClientOptions opts = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException("apiKey", "Must have an apiKey");
            if (string.IsNullOrWhiteSpace(apiSecret))
                throw new ArgumentNullException("apiSecret", "Must have an apiSecret");
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _options = opts ?? ClientOptions.Default;
            _client = new RestClient(GetBaseUrl(), TimeSpan.FromMilliseconds(_options.Timeout));
            var payload = new
            {
                server = true
            };
            _token = this.JWToken(payload);
        }

        public Users Users
        {
            get
            {
                return new Users(this);
            }
        }

        public string CreateUserToken(string userId, DateTime? expiration)
        {
            var payload = new Dictionary<string, object>
            {
                {"user_id", userId}
            };
            if (expiration.HasValue)
            {
                payload["exp"] = (Int32)(expiration.Value.ToUniversalTime().Subtract(epoch).TotalSeconds);
            }
            return this.JWToken(payload);
        }

        public async Task<ChannelTypeOutput> CreateChannelType(ChannelTypeInput channelType)
        {
            if (channelType.Commands == null || channelType.Commands.Count == 0)
            {
                channelType.Commands = new List<string>() { Commands.All };
            }
            var request = BuildAppRequest("channeltypes", HttpMethod.POST);
            request.SetJsonBody(JsonConvert.SerializeObject(channelType));

            var response = await this.MakeRequest(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Created)
                return JsonConvert.DeserializeObject<ChannelTypeOutput>(response.Content);

            throw StreamChatException.FromResponse(response);
        }

        public async Task<ChannelTypeInfo> GetChannelType(string type)
        {
            var endpoint = string.Format("channeltypes/{0}", type);
            var request = BuildAppRequest(endpoint, HttpMethod.GET);

            var response = await this.MakeRequest(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
                return JsonConvert.DeserializeObject<ChannelTypeInfo>(response.Content);

            throw StreamChatException.FromResponse(response);
        }

        public async Task<Dictionary<string, ChannelTypeInfo>> ListChannelTypes()
        {
            var request = BuildAppRequest("channeltypes", HttpMethod.GET);
            var response = await this.MakeRequest(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var obj = JObject.Parse(response.Content);
                return (Dictionary<string, ChannelTypeInfo>)obj.Property("channel_types").Value.ToObject(typeof(Dictionary<string, ChannelTypeInfo>));
            }

            throw StreamChatException.FromResponse(response);
        }

        public async Task<ChannelTypeOutput> UpdateChannelType(string type, ChannelTypeInput channelType)
        {
            var endpoint = string.Format("channeltypes/{0}", type);
            var request = BuildAppRequest(endpoint, HttpMethod.PUT);
            channelType.Name = null;
            request.SetJsonBody(JsonConvert.SerializeObject(channelType));

            var response = await this.MakeRequest(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Created)
                return JsonConvert.DeserializeObject<ChannelTypeOutput>(response.Content);

            throw StreamChatException.FromResponse(response);
        }

        public async Task DeleteChannelType(string type)
        {
            var endpoint = string.Format("channeltypes/{0}", type);
            var request = BuildAppRequest(endpoint, HttpMethod.DELETE);

            var response = await this.MakeRequest(request);

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw StreamChatException.FromResponse(response);
        }

        public async Task AddDevice(Device d)
        {
            var request = BuildAppRequest("devices", HttpMethod.POST);
            request.SetJsonBody(JsonConvert.SerializeObject(d));

            var response = await this.MakeRequest(request);
            if (response.StatusCode != System.Net.HttpStatusCode.Created)
                throw StreamChatException.FromResponse(response);
        }

        public async Task<List<Device>> GetDevices(string userID)
        {
            var request = BuildAppRequest("devices", HttpMethod.GET);
            request.AddQueryParameter("user_id", userID);
            var response = await this.MakeRequest(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var obj = JObject.Parse(response.Content);
                return (List<Device>)obj.Property("devices").Value.ToObject(typeof(List<Device>));
            }
            throw StreamChatException.FromResponse(response);
        }

        public async Task RemoveDevice(string deviceID, string userID)
        {
            var request = BuildAppRequest("devices", HttpMethod.DELETE);
            request.AddQueryParameter("user_id", userID);
            request.AddQueryParameter("id", deviceID);

            var response = await this.MakeRequest(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw StreamChatException.FromResponse(response);
        }

        public Channel Channel(string channelType, string channelID = null, GenericData data = null)
        {
            return new Channel(this, channelType, channelID, data);
        }

        internal RestRequest BuildAppRequest(string path, HttpMethod method)
        {
            return BuildRestRequest(path, method);
        }

        internal Task<RestResponse> MakeRequest(RestRequest request)
        {
            return _client.Execute(request);
        }

        internal string JWToken(object payload)
        {
            var segments = new List<string>();

            byte[] headerBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Client.JWTHeader));
            byte[] payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

            segments.Add(Base64UrlEncode(headerBytes));
            segments.Add(Base64UrlEncode(payloadBytes));

            var stringToSign = string.Join(".", segments.ToArray());
            var bytesToSign = Encoding.UTF8.GetBytes(stringToSign);

            using (var sha = new HMACSHA256(Encoding.UTF8.GetBytes(_apiSecret)))
            {
                byte[] signature = sha.ComputeHash(bytesToSign);
                segments.Add(Base64UrlEncode(signature));
            }
            return string.Join(".", segments.ToArray());
        }
        private Uri GetBaseUrl()
        {
            string region = Locations[_options.Location];
            return new Uri(string.Format(BaseUrlFormat, region));
        }

        private RestRequest BuildRestRequest(string fullPath, HttpMethod method)
        {
            var request = new RestRequest(fullPath, method);
            request.AddHeader("Authorization", _token);
            request.AddHeader("stream-auth-type", "jwt");
            request.AddHeader("X-Stream-Client", "stream-net-client");
            request.AddQueryParameter("api_key", _apiKey);
            return request;
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .Trim('=');
        }
    }
}
