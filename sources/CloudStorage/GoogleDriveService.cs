using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace CloudStorage
{
    // Google API package is doing awesome job, but.... it adds lots of dll dependecies
    // I want to keep program as dll free as it gets, so it means: REWRITE ALL THE THINGS!
    // (jk, I like doing stuff myself and learning how it works under the hood too much)

    public enum EState
    {
        NoErrors,
        NotInitialized,
        NotAuthorized,
        AuthInProgress,
        ApiFailure,
    }

    public class GoogleDriveService
    {
        private static readonly string FilesApi = "https://www.googleapis.com/drive/v3/files";
        private static readonly string UploadApi = "https://www.googleapis.com/upload/drive/v3/files";

        private readonly GoogleOAuth2.ClientIdentifier clientId;
        private GoogleOAuth2.Token authToken;
        private EState currentState;
        private string lastApiResponse;

        private Dictionary<string, string> mapFileIds;

        private class Reply
        {
            public bool bIsSuccessful;
            public string contentBody;
            public HttpWebResponse response;
        }

        public GoogleDriveService(GoogleOAuth2.ClientIdentifier clientIdentifier, GoogleOAuth2.Token savedToken)
        {
            clientId = clientIdentifier;
            authToken = savedToken;
            mapFileIds = new Dictionary<string, string>();
            currentState = ((savedToken == null) || !savedToken.IsValidForRefresh()) ? EState.NotAuthorized : EState.NotInitialized;
            lastApiResponse = null;
        }

        public GoogleOAuth2.Token GetAuthToken()
        {
            return authToken;
        }

        public EState GetState()
        {
            return currentState;
        }

        public string GetLastApiResponse()
        {
            return lastApiResponse;
        }

        public int GetFileCount()
        {
            return mapFileIds.Count;
        }

        public async Task InitFileList()
        {
            string requestUri = FilesApi +
                "?list" +
                "&spaces=appDataFolder" +
                "&q=" + Uri.EscapeDataString("trashed=false");

            bool bHasValidResponse = false;

            Reply reply = await HandleRequest("GET", requestUri);
            if (reply.bIsSuccessful)
            {
                JsonParser.ObjectValue jsonOb = JsonParser.ParseJson(reply.contentBody);
                if (jsonOb != null)
                {
                    JsonParser.ArrayValue fileArr = (JsonParser.ArrayValue)jsonOb["files"];
                    foreach (JsonParser.Value entry in fileArr.entries)
                    {
                        JsonParser.ObjectValue entryOb = (JsonParser.ObjectValue)entry;

                        string mapKey = entryOb["name"];
                        mapFileIds.Remove(mapKey);
                        mapFileIds.Add(mapKey, entryOb["id"]);
                    }

                    bHasValidResponse = true;
                }
            }

            UpdateCurrentState(bHasValidResponse);
        }

        public async Task<bool> UploadTextFile(string fileName, string fileContent)
        {
            bool bResult = false;
            if (!mapFileIds.ContainsKey(fileName))
            {
                string uploadRequestUri = UploadApi + "?uploadType=multipart";
                string uploadMeta = "{\"name\":\"" + fileName + "\",parents:[\"appDataFolder\"]}";

                Reply reply = await HandleRequest("POST", uploadRequestUri, uploadMeta, fileContent);
                if (reply.bIsSuccessful)
                {
                    JsonParser.ObjectValue jsonOb = JsonParser.ParseJson(reply.contentBody);
                    if (jsonOb != null)
                    {
                        string fileId = jsonOb["id"];
                        mapFileIds.Remove(fileName);
                        mapFileIds.Add(fileName, fileId);
                        bResult = true;
                    }
                }
            }
            else
            {
                string patchRequestUri = UploadApi + 
                    "/" + mapFileIds[fileName] +
                    "?uploadType=media";

                Reply reply = await HandleRequest("PATCH", patchRequestUri, fileContent);
                bResult = reply.bIsSuccessful;
            }

            UpdateCurrentState(bResult);
            return bResult;
        }

        public async Task<string> DownloadTextFile(string fileName)
        {
            if (!mapFileIds.ContainsKey(fileName))
            {
                return null;
            }

            string getRequestUri = FilesApi +
                "/" + mapFileIds[fileName] +
                "?alt=media";

            Reply reply = await HandleRequest("GET", getRequestUri);
            if (reply.bIsSuccessful)
            {
                UpdateCurrentState(true);
                return reply.contentBody;
            }

            UpdateCurrentState(false);
            return null;
        }

        private void UpdateCurrentState(bool bHasValidApiResponse)
        {
            if (currentState <= EState.NotInitialized && !bHasValidApiResponse)
            {
                currentState = EState.ApiFailure;
            }
        }

        private async Task<Reply> HandleRequest(string method, string requestUri, params string[] requestBody)
        {
            Reply result = new Reply();
            result.bIsSuccessful = false;

            currentState = EState.AuthInProgress;

            GoogleOAuth2.Token activeToken = await GoogleOAuth2.GetAuthorizationToken(clientId, authToken);
            if (activeToken != null && activeToken.IsValidForAuth())
            {
                authToken = activeToken;
                currentState = EState.NoErrors;

                HttpWebRequest request = WebRequest.CreateHttp(requestUri);
                request.Method = method;
                request.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + authToken.accessToken);
                SetRequestContent(request, requestBody);

                WebResponse rawResponse = null;
                try
                {
                    rawResponse = await request.GetResponseAsync();
                }
                catch (WebException ex)
                {
                    lastApiResponse = ex.Message;
                }
                catch (Exception ex)
                {
                    lastApiResponse = "Exception: " + ex;
                }

                HttpWebResponse response = (HttpWebResponse)rawResponse;
                result.response = response;

                if (response != null)
                {
                    lastApiResponse = response.StatusDescription;
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string responseBody = null;
                        if (response.ContentLength > 0)
                        {
                            byte[] contentBytes = new byte[response.ContentLength];
                            Stream contentStream = response.GetResponseStream();
                            contentStream.Read(contentBytes, 0, contentBytes.Length);
                            contentStream.Close();

                            responseBody = Encoding.UTF8.GetString(contentBytes);
                        }

                        result.bIsSuccessful = true;
                        result.contentBody = responseBody;
                    }
                }
            }
            else
            {
                currentState = EState.NotAuthorized;
            }

            return result;
        }

        private void SetRequestContent(HttpWebRequest request, string[] parts)
        {
            byte[] contentBytes = null;

            if (parts.Length == 0)
            {
                request.ContentType = "application/json; charset=UTF-8";
            }
            else if (parts.Length == 1)
            {
                bool bJsonRequest = (parts[0].Length > 1) && (parts[0][0] == '{');
                request.ContentType = (bJsonRequest ? "application/json" : "text/plain") + "; charset=UTF-8";

                contentBytes = Encoding.UTF8.GetBytes(parts[0]);
            }
            else
            {
                string boundaryStr = "SPLITMEHERE";
                request.ContentType = "multipart/related; boundary=" + boundaryStr;

                MemoryStream memoryStream = new MemoryStream();
                foreach (string str in parts)
                {
                    bool bJsonRequest = (str.Length > 1) && (str[0] == '{');
                    string header = (memoryStream.Position == 0 ? "" : "\r\n") + "--" + boundaryStr + "\r\nContent-Type: " + (bJsonRequest ? "application/json" : "text/plain") + "; charset=UTF-8\r\n\r\n";

                    byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                    memoryStream.Write(headerBytes, 0, headerBytes.Length);

                    byte[] partBytes = Encoding.UTF8.GetBytes(str);
                    memoryStream.Write(partBytes, 0, partBytes.Length);
                }

                string footer = "\r\n--" + boundaryStr + "--";
                byte[] footerBytes = Encoding.UTF8.GetBytes(footer);
                memoryStream.Write(footerBytes, 0, footerBytes.Length);

                contentBytes = new byte[memoryStream.Length];
                memoryStream.Position = 0;
                memoryStream.Read(contentBytes, 0, contentBytes.Length);
                memoryStream.Close();
            }

            request.ContentLength = (contentBytes != null) ? contentBytes.Length : 0;
            if (contentBytes != null)
            {
                Stream contentStream = request.GetRequestStream();
                contentStream.Write(contentBytes, 0, contentBytes.Length);
                contentStream.Close();
            }
        }
    }
}
