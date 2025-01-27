using Microsoft.IdentityModel.Tokens;
using PinquarkWMSSynchro.Models;
using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Threading;
using System.Xml.Linq;
using Newtonsoft.Json.Serialization;
using Serilog;
using System.IO;

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
        private readonly HttpClient _httpClient;

        private string _cachedToken;
        private DateTime _tokenExpirationTime;
        private readonly ILogger _logger;

        public RestApiClient(HttpClient httpClient, ILogger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

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
                var authUrl = $"{_baseUrl}/auth/sign-in";

                var authData = new
                {
                    username = _username,
                    password = _password
                };

                var content = new StringContent(JsonConvert.SerializeObject(authData), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(authUrl, content);
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
            catch (Exception ex)
            {
                throw ex;
            }

        }
        public Task<int> SendDocumentAsync(Document document) => PostAsync("documents", document);
        public Task<int> SendProductAsync(Product product) => PostAsync("articles", product);
        public Task<int> SendClientAsync(Client client) => PostAsync("contractors", client);

        private async Task<int> PostAsync<T>(string endpoint, T payload)
        {
            try
            {
                var authToken = await GetAuthTokenAsync();

                var json = JsonConvert.SerializeObject(payload, _jsonSettings);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

                var response = await _httpClient.PostAsync($"{_baseUrl}/{endpoint}", content);
                SavePayloadToFile(endpoint, json);

                if (response.IsSuccessStatusCode)
                {
                    return 1; // Successfully sent
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to send {typeof(T).Name}. Response: {errorResponse}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sending payload of type {PayloadType}. Payload: {Payload}. Error: {ErrorMessage}",
                      typeof(T).Name,
                      JsonConvert.SerializeObject(payload, Formatting.None),
                      ex.Message);
                throw;
            }
        }
        private void SavePayloadToFile(string endpoint, string json)
        {
            try
            {
                var logsDirectory = $@"{AppDomain.CurrentDomain.BaseDirectory}\logs\json\{endpoint}";
                if (!Directory.Exists(logsDirectory))
                {
                    Directory.CreateDirectory(logsDirectory);
                }

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var fileName = $"{endpoint}_{timestamp}.json";
                var filePath = Path.Combine(logsDirectory, fileName);

                File.WriteAllText(filePath, json);
                _logger.Information("Payload successfully saved to file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save payload to file.");
            }

        }
    }
}
