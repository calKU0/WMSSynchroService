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
        public Task<int> SendDocumentAsync(Document document) => PostAsync("documents", document, false);
        public Task<int> SendProductsAsync(List<Product> products) => PostAsync("articles", products, true);
        public Task<int> SendClientsAsync(List<Client> clients) => PostAsync("contractors", clients, true);
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
        private async Task<int> PostAsync<T>(string endpoint, T payload, bool isList)
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    string fullEndpoint = endpoint;
                    if (isList)
                    {
                        fullEndpoint += "/list";
                    }

                    var authToken = await GetAuthTokenAsync();

                    var json = JsonConvert.SerializeObject(payload, _jsonSettings);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                    var response = await httpClient.PostAsync($"{_baseUrl}/{fullEndpoint}", content);
                    SavePayloadToFile(endpoint, json);

                    if (response.IsSuccessStatusCode)
                    {
                        return 1;
                    }
                    else
                    {
                        var errorResponse = await response.Content.ReadAsStringAsync();
                        await LogToTable(payload, endpoint.Replace("s", "").ToUpper(), 0, $"Failed to send {typeof(T).Name}. Response: {errorResponse}");
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
                return 0;
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

                        if (feedbackList.Any())
                        {
                            SavePayloadToFile("feedback", responseContent, "feedback");

                            foreach (Feedback feedback in feedbackList)
                            {
                                var feedbackJson = JsonConvert.SerializeObject(feedback, _jsonSettings);
                                await LogToTable(feedback.Id, feedback.Entity, Convert.ToInt32(feedback.Success), feedback.Errors.FirstOrDefault().Value);
                            }
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
                string fileName = $"{endpoint}_{timestamp:yyyy_MM_dd_HH_mm_ss_fff}.json";
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
            string status;

            if (obj is Document document)
            {
                await _database.LogToTable(document.ErpId, document.ErpType, endpoint, success, error);
                status = success == 1 ? "Zsynchronizowano" : "Błąd synchronizacji";

                int result = await _database.UpdateAttribute(document.ErpId, document.ErpType, "StatusWMS", status);
                if (result != 0)
                {
                    _logger.Error("Error while updating attribute StatusWMS for Document: " + result.ToString());
                }
            }
            else if (obj is List<Product> products)
            {
                var tasks = products.Select(async p =>
                {
                    await _database.LogToTable(p.ErpId, 16, endpoint, success, error);
                    string productStatus = success == 1 ? "Zsynchronizowano" : "Błąd synchronizacji";
                    int result = await _database.UpdateAttribute(p.ErpId, 16, "StatusWMS", productStatus);
                    if (result != 0)
                    {
                        _logger.Error($"Error while updating attribute StatusWMS for Product {p.ErpId}: " + result.ToString());
                    }
                });
                await Task.WhenAll(tasks);
            }
            else if (obj is List<Client> clients)
            {
                var tasks = clients.Select(async client =>
                {
                    await _database.LogToTable(client.ErpId, 32, endpoint, success, error);
                    string clientStatus = success == 1 ? "Zsynchronizowano" : "Błąd synchronizacji";
                    int result = await _database.UpdateAttribute(client.ErpId, 32, "StatusWMS", clientStatus);
                    if (result != 0)
                    {
                        _logger.Error($"Error while updating attribute StatusWMS for Client {client.ErpId}: " + result.ToString());
                    }
                });
                await Task.WhenAll(tasks);
            }
            else
            {
                int type;
                int id;

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
                        id = Convert.ToInt32(obj.ToString().Split('|')[0]);
                        type = Convert.ToInt32(obj.ToString().Split('|')[1]);
                        break;
                    default:
                        id = Convert.ToInt32(obj);
                        type = 0;
                        break;
                }

                status = success == 1 ? "Zsynchronizowano" : "Błąd synchronizacji";

                int result = await _database.UpdateAttribute(id, type, "StatusWMS", status);
                if (result != 0)
                {
                    _logger.Error($"Error while updating attribute StatusWMS for ID {id}: " + result.ToString());
                }

                await _database.LogToTable(id, type, endpoint, success, error);
            }
        }
    }
}
