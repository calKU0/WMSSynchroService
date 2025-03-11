using Microsoft.IdentityModel.Tokens;
using PinquarkWMSSynchro.Models;
using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Xml.Linq;
using Newtonsoft.Json.Serialization;
using Serilog;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Text.RegularExpressions;

namespace PinquarkWMSSynchro.Infrastructure
{
    public class RestApiClient
    {
        private readonly string _baseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"];
        private readonly string _username = ConfigurationManager.AppSettings["ApiUsername"];
        private readonly string _password = ConfigurationManager.AppSettings["ApiPassword"];
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented
        };
        private readonly DatabaseRepository _database;

        private string _cachedToken;
        private DateTime _tokenExpirationTime;
        private readonly ILogger _logger;

        public RestApiClient(DatabaseRepository database, ILogger logger)
        {
            _logger = logger;
            _database = database;
        }
        public Task<int> SendDocumentAsync(Document document) => PostAsync("documents", document);
        public Task<int> SendProductAsync(Product product) => PostAsync("articles", product);
        public Task<int> SendClientAsync(Client client) => PostAsync("contractors", client);
        private async Task<string> GetAuthTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpirationTime)
            {
                return _cachedToken;
            }

            _cachedToken = await FetchAuthTokenAsync();
            return _cachedToken;
        }

        private async Task<string> FetchAuthTokenAsync()
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    var authUrl = $"{_baseUrl}/auth/sign-in";

                    var authData = new
                    {
                        username = _username,
                        password = _password
                    };

                    var content = new StringContent(JsonConvert.SerializeObject(authData), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(authUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        var tokenResponse = await response.Content.ReadAsStringAsync();
                        dynamic jsonResponse = JsonConvert.DeserializeObject(tokenResponse);
                        var token = jsonResponse.accessToken.ToString();
                        var expirationDateString = jsonResponse.accessTokenExpirationDate.ToString();
                        var expirationDate = DateTimeOffset.Parse(expirationDateString).UtcDateTime;
                        _tokenExpirationTime = expirationDate.AddSeconds(-120);

                        return token;
                    }
                    else
                    {
                        throw new Exception("Unable to retrieve authentication token.");
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
        private async Task<int> PostAsync<T>(string endpoint, T payload)
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    var authToken = await GetAuthTokenAsync();

                    var json = JsonConvert.SerializeObject(payload, _jsonSettings);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

                    var response = await httpClient.PostAsync($"{_baseUrl}/{endpoint}", content);
                    SavePayloadToFile(endpoint, json);

                    if (response.IsSuccessStatusCode)
                    {
                        return 1;
                    }
                    else
                    {
                        var errorResponse = await response.Content.ReadAsStringAsync();
                        await LogToTable(payload, endpoint, 0, $"Failed to send {typeof(T).Name}. Response: {errorResponse}");
                        throw new Exception($"Failed to send {typeof(T).Name}. Response: {errorResponse}");
                    }
                }
            }
            catch (Exception ex)
            {
                await LogToTable(payload, endpoint.Replace("s", "").ToUpper(), 0, $"Error sending payload {typeof(T).Name}. Error: {ex.Message}");
                _logger.Error(ex, "Error sending payload of type {PayloadType}. Payload: {Payload}. Error: {ErrorMessage}",
                      typeof(T).Name,
                      JsonConvert.SerializeObject(payload, Formatting.None),
                      ex.Message);
                throw;
            }
        }
        public async Task GetFeedbackAsync(bool delete)
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    var authToken = await GetAuthTokenAsync();
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

                    var query = $"?delete={delete}";
                    var url = $"{_baseUrl}/feedbacks{query}";
                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var feedbackList = JsonConvert.DeserializeObject<List<Feedback>>(responseContent, _jsonSettings);

                        SavePayloadToFile("feedback", responseContent, "feedback");
                        foreach (Feedback feedback in feedbackList)
                        {
                            var feedbackJson = JsonConvert.SerializeObject(feedback, _jsonSettings);
                            await LogToTable(feedback.Id, feedback.Entity, Convert.ToInt32(feedback.Success), feedback.Errors.FirstOrDefault().Value);
                        }
                    }
                    else
                    {
                        var errorResponse = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Failed to retrieve feedback. Response: {errorResponse}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feedbacks");
                throw;
            }
        }
        private void SavePayloadToFile(string endpoint, string json, string entity = "")
        {
            try
            {
                string logsDirectory = $@"{AppDomain.CurrentDomain.BaseDirectory}\logs\json\{endpoint}";

                if (!Directory.Exists(logsDirectory))
                {
                    Directory.CreateDirectory(logsDirectory);
                }

                var timestamp = DateTime.UtcNow.ToString("yyyy_MM_dd_HH_mm_ss_fff");

                JToken jsonToken = JToken.Parse(json);
                string symbol = null;

                if (jsonToken is JArray jsonArray && jsonArray.Count > 0)
                {
                    symbol = jsonArray[0]["symbol"]?.ToString();
                }
                else if (jsonToken is JObject jsonObject)
                {
                    symbol = jsonObject["symbol"]?.ToString();
                }

                string fileName;
                if (!string.IsNullOrEmpty(symbol))
                {
                    symbol = SanitizeFileName(symbol);
                    fileName = $"{symbol}_{timestamp:yyyy_MM_dd_HH_mm_ss_fff}.json";
                }
                else
                {
                    fileName = $"{endpoint}_{timestamp:yyyy_MM_dd_HH_mm_ss_fff}.json";
                }

                var filePath = Path.Combine(logsDirectory, fileName);

                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save payload to file.");
            }
        }

        private string SanitizeFileName(string fileName)
        {
            return Regex.Replace(fileName, @"[<>:""/\\|?*]", "_");
        }

        private async Task LogToTable(object obj, string endpoint, int success, string error = "")
        {
            if (obj is Document document)
            {
                await _database.LogToTable(document.ErpId, document.ErpType, endpoint, success, error);
            }
            else if (obj is Product product)
            {
                await _database.LogToTable(product.ErpId, 16, endpoint, success, error);
            }
            else if (obj is Client client)
            {
                await _database.LogToTable(client.ErpId, 32, endpoint, success, error);
            }
            else
            {
                int type;
                int id;
                string status;
                switch (endpoint)
                {
                    case "ARTICLE":
                        id = Convert.ToInt32(obj);
                        type = 16;
                        break;
                    case "CONTRACTOR":
                        id = Convert.ToInt32(obj);
                        type = 32;
                        break;
                    case "DOCUMENT":
                        id = Convert.ToInt32(obj.ToString().Split('|')[1]);
                        type = Convert.ToInt32(obj.ToString().Split('|')[0]);
                        break;
                    default:
                        id = Convert.ToInt32(obj);
                        type = 0;
                        break;
                }
                if (success == 1)
                    status = "Zsynchronizowano";
                else
                    status = "Błąd synchronizacji";


                int result = await _database.UpdateAttribute(id, type, "StatusWMS", status);
                if (result != 0)
                {
                    _logger.Error("Error while updating attribute StatusWMS: " + result.ToString());
                }
                await _database.LogToTable(id, type, endpoint, success, error);
            }
        }
    }
}
