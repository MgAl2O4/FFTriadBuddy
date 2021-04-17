using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MgAl2O4.GoogleAPI
{
    // Google API package is doing awesome job, but.... it adds lots of dll dependecies
    // I want to keep program as dll free as it gets, so it means: REWRITE ALL THE THINGS!
    // (jk, I like doing stuff myself and learning how it works under the hood too much)
    //
    // source of API and params: Google OAuth 2.0 Playground

    public class GoogleOAuth2
    {
        public class Token
        {
            public DateTime expirationDate;
            public string accessToken;
            public string refreshToken;

            public bool IsValidForAuth()
            {
                return !string.IsNullOrEmpty(accessToken) && (expirationDate != null) && (DateTime.Now.CompareTo(expirationDate) < 0);
            }

            public bool IsValidForRefresh()
            {
                return !string.IsNullOrEmpty(refreshToken);
            }

            public override string ToString()
            {
                return "AccessToken:" + accessToken + ", RefreshToken:" + refreshToken + ", ExpirationDate:" + expirationDate;
            }
        }

        public class ClientIdentifier
        {
            public override string ToString()
            {
                return "ID:" + GetID() + ", Secret:" + GetSecret();
            }

            public virtual string GetID() { return ""; }
            public virtual string GetSecret() { return ""; }
        }

        private static readonly string RequestAccessApi = "https://accounts.google.com/o/oauth2/v2/auth";
        private static readonly string TokenApi = "https://www.googleapis.com/oauth2/v4/token";
        //private static readonly string AccessScopeOwnedFiles = "https://www.googleapis.com/auth/drive.file";
        private static readonly string AccessScopeAppSettings = "https://www.googleapis.com/auth/drive.appdata";
        private static HttpListener httpListener = new HttpListener();

        public static void KillPendingAuthorization()
        {
            if (httpListener != null && httpListener.IsListening)
            {
                httpListener.Close();
                httpListener = null;
            }
        }

        public static async Task<Token> GetAuthorizationToken(ClientIdentifier clientIdentifier, Token savedTokenData)
        {
            if (clientIdentifier == null)
            {
                return null;
            }

            if (savedTokenData != null)
            {
                if (savedTokenData.IsValidForAuth())
                {
                    return savedTokenData;
                }
                else if (savedTokenData.IsValidForRefresh())
                {
                    Token refreshedToken = await RefreshToken(clientIdentifier, savedTokenData);
                    return refreshedToken;
                }
            }

            KillPendingAuthorization();

            Token newToken = await RequestToken(clientIdentifier);
            return newToken;
        }

        private static async Task<Token> RequestToken(ClientIdentifier clientIdentifier)
        {
            // prepare listen server for receiving token data
            if (!HttpListener.IsSupported || clientIdentifier == null)
            {
                return null;
            }

            int listenPort = FindListenPort();
            string authListenUri = "http://localhost:" + listenPort + "/auth_response/";

            httpListener = new HttpListener();
            httpListener.Prefixes.Add(authListenUri);
            httpListener.Start();

            // send authorization request: open in default browser
            string authRequestUri = RequestAccessApi +
                "?redirect_uri=" + Uri.EscapeDataString(authListenUri) +
                "&prompt=consent" +
                "&response_type=code" +
                "&client_id=" + clientIdentifier.GetID() +
                "&scope=" + Uri.EscapeDataString(AccessScopeAppSettings) +
                "&access_type=offline";

            Process.Start(authRequestUri);

            // wait for reponse
            string authorizationCode = null;
            {
                HttpListenerContext listenContext = null;
                try
                {
                    listenContext = await Task.Factory.FromAsync(httpListener.BeginGetContext(null, null), httpListener.EndGetContext);
                }
                catch (Exception) { listenContext = null; }

                if (listenContext != null)
                {
                    Uri requestUrl = listenContext.Request.Url;

                    ILookup<string, string> queryLookup = requestUrl.Query.TrimStart('?')
                        .Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Split('='))
                        .Where(k => k.Length == 2)
                        .ToLookup(a => a[0], a => Uri.UnescapeDataString(a[1]), StringComparer.OrdinalIgnoreCase);
                    authorizationCode = queryLookup["code"].FirstOrDefault();

                    string responseString = "<html><title>Google account verification</title><body>Received verification code. You may now close this window.</body></html>";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                    HttpListenerResponse listenResponse = listenContext.Response;
                    listenResponse.ContentLength64 = buffer.Length;
                    listenResponse.OutputStream.Write(buffer, 0, buffer.Length);
                    listenResponse.OutputStream.Close();
                }
            }

            // send token grant request: no user ui needed
            Token resultToken = null;
            if (!string.IsNullOrEmpty(authorizationCode))
            {
                HttpContent tokenGrantContent = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("code", authorizationCode),
                new KeyValuePair<string, string>("redirect_uri", authListenUri),
                new KeyValuePair<string, string>("client_id", clientIdentifier.GetID()),
                new KeyValuePair<string, string>("client_secret", clientIdentifier.GetSecret()),
                new KeyValuePair<string, string>("scope", AccessScopeAppSettings),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                });

                tokenGrantContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                try
                {

                    HttpClient client = new HttpClient();
                    HttpResponseMessage tokenGrantResponse = await client.PostAsync(TokenApi, tokenGrantContent);
                    if (tokenGrantResponse.IsSuccessStatusCode)
                    {
                        string replyJson = await tokenGrantResponse.Content.ReadAsStringAsync();
                        resultToken = CreateToken(replyJson);
                    }
                }
                catch (Exception) { }
            }

            if (httpListener != null && httpListener.IsListening)
            {
                httpListener.Close();
                httpListener = null;
            }

            return resultToken;
        }

        private static async Task<Token> RefreshToken(ClientIdentifier clientIdentifier, Token tokenData)
        {
            Token resultToken = null;

            HttpContent requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_secret", clientIdentifier.GetSecret()),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", tokenData.refreshToken),
                new KeyValuePair<string, string>("client_id", clientIdentifier.GetID()),
                });

            requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            try
            {
                HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.PostAsync(TokenApi, requestContent);
                if (response.IsSuccessStatusCode)
                {
                    string replyJson = await response.Content.ReadAsStringAsync();
                    resultToken = CreateToken(replyJson);
                    resultToken.refreshToken = tokenData.refreshToken;
                }
            }
            catch (Exception) { }

            return resultToken;
        }

        private static Token CreateToken(string jsonStr)
        {
            JsonParser.ObjectValue jsonOb = JsonParser.ParseJson(jsonStr);
            int validForSec = (JsonParser.IntValue)jsonOb["expires_in"];

            return new Token
            {
                accessToken = jsonOb["access_token"],
                refreshToken = jsonOb["refresh_token", JsonParser.StringValue.Empty],
                expirationDate = DateTime.Now.AddSeconds(validForSec)
            };
        }

        private static int FindListenPort()
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();
            return port;
        }
    }
}
