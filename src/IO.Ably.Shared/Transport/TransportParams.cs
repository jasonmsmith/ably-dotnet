﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IO.Ably.Transport
{
    /// <summary>
    /// Parameters passed when creating a new Websocket transport.
    /// </summary>
    public class TransportParams
    {
        internal static Regex RecoveryKeyRegex { get; set; } = new Regex(@"^([\w!-]+):(-?\d+):(-?\d+)$");

        internal ILogger Logger { get; private set; }

        /// <summary>
        /// Host used to establish the connection.
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        /// Use a secure connection.
        /// </summary>
        public bool Tls { get; private set; }

        /// <summary>
        /// A list of fallback hosts.
        /// </summary>
        public string[] FallbackHosts { get; private set; }

        /// <summary>
        /// Connection port.
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// Connection key.
        /// </summary>
        public string ConnectionKey { get; private set; }

        /// <summary>
        /// Connection serial.
        /// </summary>
        public long? ConnectionSerial { get; set; }

        /// <summary>
        /// Whether to use the binary protocol.
        /// </summary>
        public bool UseBinaryProtocol { get; private set; }

        internal AuthMethod AuthMethod { get; private set; }

        /// <summary>
        /// Either ably key or token value.
        /// </summary>
        public string AuthValue { get; private set; } // either key or token

        /// <summary>
        /// Recover value used to recover a connection.
        /// </summary>
        public string RecoverValue { get; private set; }

        /// <summary>
        /// Id of the client establishing the connection.
        /// </summary>
        public string ClientId { get; private set; }

        /// <summary>
        /// Whether to echo message.
        /// </summary>
        public bool EchoMessages { get; private set; }

        /// <summary>
        /// Additional parameters coming from ClientOptions.
        /// </summary>
        public Dictionary<string, string> AdditionalParameters { get; set; }

        private TransportParams()
        {
        }

        internal static async Task<TransportParams> Create(string host, AblyAuth auth, ClientOptions options, string connectionKey = null, long? connectionSerial = null, ILogger logger = null)
        {
            var result = new TransportParams();
            result.Host = host;
            result.Tls = options.Tls;
            result.Port = options.Tls ? options.TlsPort : options.Port;
            result.ClientId = options.GetClientId();
            result.AuthMethod = auth.AuthMethod;
            if (result.AuthMethod == AuthMethod.Basic)
            {
                result.AuthValue = ApiKey.Parse(options.Key).ToString();
            }
            else
            {
                var token = await auth.GetCurrentValidTokenAndRenewIfNecessaryAsync();
                if (token == null)
                {
                    throw new AblyException("There is no valid token. Can't authenticate", 40100, HttpStatusCode.Unauthorized);
                }

                result.AuthValue = token.Token;
            }

            result.ConnectionKey = connectionKey;
            result.ConnectionSerial = connectionSerial;
            result.EchoMessages = options.EchoMessages;
            result.FallbackHosts = options.FallbackHosts;
            result.UseBinaryProtocol = options.UseBinaryProtocol;
            result.RecoverValue = options.Recover;
            result.Logger = logger ?? options.Logger;
            result.AdditionalParameters = StringifyParameters(options.TransportParams);
            return result;

            Dictionary<string, string> StringifyParameters(Dictionary<string, object> originalParams)
            {
                if (originalParams is null)
                {
                    return new Dictionary<string, string>();
                }

                return originalParams.ToDictionary(x => x.Key, x => ConvertValue(x.Key, x.Value));

                string ConvertValue(string key, object value)
                {
                    switch (value)
                    {
                        case bool boolValue:
                            return boolValue.ToString().ToLower();
                        case null:
                            return string.Empty;
                        default:
                            try
                            {
                                return value.ToString();
                            }
                            catch (Exception e)
                            {
                                logger.Error($"Error converting custom transport parameter '{key}'. Error: {e.Message}");
                                return string.Empty;
                            }
                    }
                }
            }
        }

        /// <summary>
        /// Helper method used to construct the server uri.
        /// </summary>
        /// <returns>Server uri.</returns>
        public Uri GetUri()
        {
            var wsScheme = Tls ? "wss://" : "ws://";
            var uriBuilder = new UriBuilder(wsScheme, Host, Port);
            uriBuilder.Query = GetParams().ToQueryString();
            return uriBuilder.Uri;
        }

        /// <summary>
        /// Gets the current query parameters a dictionary.
        /// </summary>
        /// <returns>dictionary of query parameters.</returns>
        public Dictionary<string, string> GetParams()
        {
            var result = new Dictionary<string, string>();

            if (AuthMethod == AuthMethod.Basic)
            {
                result["key"] = AuthValue;
            }
            else
            {
                result["accessToken"] = AuthValue;
            }

            result["v"] = Defaults.ProtocolVersion;
            result["lib"] = Defaults.LibraryVersion;

            // Url encode all the params at the time of creating the query string
            result["format"] = UseBinaryProtocol ? "msgpack" : "json";
            result["echo"] = EchoMessages.ToString().ToLower();

            if (ConnectionKey.IsNotEmpty())
            {
                result["resume"] = ConnectionKey;
                if (ConnectionSerial.HasValue)
                {
                    result["connection_serial"] = ConnectionSerial.Value.ToString();
                }
            }
            else if (RecoverValue.IsNotEmpty())
            {
                var match = RecoveryKeyRegex.Match(RecoverValue);
                if (match.Success)
                {
                    result["recover"] = match.Groups[1].Value;
                    result["connection_serial"] = match.Groups[2].Value;
                }
            }

            if (ClientId.IsNotEmpty())
            {
                result["clientId"] = ClientId;
            }

            if (AdditionalParameters?.Any() ?? false)
            {
                return AdditionalParameters.Merge(result);
            }

            return result;
        }
    }
}
