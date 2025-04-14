using Microsoft.Data.SqlClient;
using PinquarkWMSSynchro.Infrastructure;
using PinquarkWMSSynchro.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Xml.Linq;

namespace PinquarkWMSSynchro
{
    public class DatabaseRepository
    {
        private readonly string _connectionString;
        private readonly XlApiService _xlApiService;
        private readonly ILogger _logger;

        public DatabaseRepository(string connectionString, XlApiService xlApiService, ILogger logger)
        {
            _connectionString = connectionString;
            _xlApiService = xlApiService;
            _logger = logger;
        }

        public async Task<List<Document>> GetDocumentsAsync()
        {
            List<Document> documents = new List<Document>();
            string procedureName = "kkur.WMSPobierzDokumenty";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (SqlCommand command = new SqlCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    using (SqlDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        List<Task<int>> updateTasks = new List<Task<int>>();
                        List<Task<List<DocumentPosition>>> positionTasks = new List<Task<List<DocumentPosition>>>();
                        List<Task<List<Models.Attribute>>> attributeTasks = new List<Task<List<Models.Attribute>>>();

                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            int trnNumer = Convert.ToInt32(reader["TrnNumer"]);
                            int trnTyp = Convert.ToInt32(reader["TrnTyp"]);

                            try
                            {
                                updateTasks.Add(UpdateAttribute(trnNumer, trnTyp, "StatusWMS", "Przetwarzane"));
                                positionTasks.Add(GetDocumentElementsAsync(trnNumer, trnTyp));
                                attributeTasks.Add(GetDocumentAttributesAsync(trnNumer, trnTyp));

                                Document document = new Document()
                                {
                                    ErpId = trnNumer,
                                    ErpIdTxt = trnNumer + "|" + trnTyp,
                                    ErpType = trnTyp,
                                    DocumentType = reader["DokumentTyp"].ToString(),
                                    ErpCode = reader["PelnaNazwa"].ToString(),
                                    ErpStatusSymbol = reader["Status"].ToString(),
                                    OwnCode = reader["PelnaNazwa"].ToString(),
                                    InputDocumentNumber = reader["PelnaNazwa"].ToString(),
                                    Source = "ERP",
                                    Symbol = reader["Kod"].ToString(),
                                    Date = reader["Data"].ToString(),
                                    Note = reader["Opis"].ToString(),
                                    DeliveryMethodSymbol = reader["SposobDostawy"].ToString(),
                                    Priority = Convert.ToInt32(reader["Priorytet"] == DBNull.Value ? null : reader["Priorytet"]),
                                    WarehouseSymbol = reader["Magazyn"].ToString(),
                                    ReciepentId = Convert.ToInt32(reader["KntNumerOdbiorcy"]),
                                    ReciepentSource = "ERP",

                                    Contractor = new DocumentClient()
                                    {
                                        ErpId = Convert.ToInt32(reader["KntNumer"]),
                                        Source = "ERP"
                                    },

                                    DeliveryAddress = new ClientAddress()
                                    {
                                        Active = true,
                                        ContractorId = Convert.ToInt32(reader["KntNumer"]),
                                        ContractorSource = "ERP",
                                        Code = reader["KodPocztowy"].ToString(),
                                        Name = reader["AdresNazwa"].ToString(),
                                        PostCity = reader["Miasto"].ToString(),
                                        City = reader["Miasto"].ToString(),
                                        Street = reader["Ulica"].ToString(),
                                        Country = reader["Kraj"].ToString(),
                                        DateFrom = reader["DataOd"].ToString(),
                                    }
                                };

                                documents.Add(document);
                            }
                            catch (Exception ex)
                            {
                                await LogToTable(trnNumer, trnTyp, "DOCUMENT", 0, "Error while fetching document from database. Error Message:" + ex.Message);
                                int resultUpdate = await UpdateAttribute(trnNumer, trnTyp, "StatusWMS", "Błąd synchronizacji");
                                if (resultUpdate != 0)
                                {
                                    _logger.Error("Error while updating attribute StatusWMS for document: " + resultUpdate.ToString());
                                }

                                _logger.Error(ex, "Error while fetching document from database");
                            }
                        }

                        await Task.WhenAll(updateTasks).ConfigureAwait(false);
                        var positionResults = await Task.WhenAll(positionTasks).ConfigureAwait(false);
                        var attributeResults = await Task.WhenAll(attributeTasks).ConfigureAwait(false);

                        for (int i = 0; i < documents.Count; i++)
                        {
                            documents[i].Positions = positionResults[i];
                            documents[i].Attributes = attributeResults[i];
                        }
                    }
                }
            }

            return documents;
        }

        public async Task<List<DocumentPosition>> GetDocumentElementsAsync(int documentId, int documentType)
        {
            List<DocumentPosition> positions = new List<DocumentPosition>();
            try
            {
                string procedureName = "kkur.WMSPobierzElementyDokumentu";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(procedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("@GidNumer", SqlDbType.Int).Value = documentId;
                        command.Parameters.Add("@GidTyp", SqlDbType.Int).Value = documentType;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                DocumentPosition position = new DocumentPosition()
                                {
                                    ErpId = Convert.ToInt32(reader["TwrNumer"]),
                                    Quantity = Convert.ToInt32(reader["Ilosc"]),
                                    StatusSymbol = reader["Status"].ToString(),
                                    No = Convert.ToInt32(reader["Lp"]),
                                    Note = reader["Opis"].ToString(),
                                    Article = new DocumentElement()
                                    {
                                        ErpId = Convert.ToInt32(reader["TwrNumer"]),
                                        Source = "ERP",
                                        Unit = reader["Jm"].ToString()
                                    }

                                };

                                positions.Add(position);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogToTable(documentId, documentType, "DOCUMENT", 0, "Error while fetching document elements from database. Error Message:" + ex.Message);
                int resultUpdate = await UpdateAttribute(documentId, documentType, "StatusWMS", "Błąd synchronizacji");
                if (resultUpdate != 0)
                {
                    _logger.Error("Error while updating attribute StatusWMS for document: " + resultUpdate.ToString());
                }

                _logger.Error(ex, "Error while fetching document elements from database");
                throw new ProcessingException();
            }

            return positions;
        }

        private async Task<List<Models.Attribute>> GetDocumentAttributesAsync(int prodcuctId, int productType)
        {
            List<Models.Attribute> attributes = new List<Models.Attribute>();
            try
            {
                string procedureName = "kkur.WMSPobierzAtrybutyDokumentu";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(procedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("@GidNumer", SqlDbType.Int).Value = prodcuctId;
                        command.Parameters.Add("@GidTyp", SqlDbType.Int).Value = productType;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Models.Attribute attribute = new Models.Attribute()
                                {
                                    Symbol = reader["Klasa"].ToString(),
                                    Type = reader["Typ"].ToString(),
                                };

                                switch (attribute.Type.ToLower())
                                {
                                    case "integer":
                                        attribute.ValueInt = Convert.ToInt32(reader["Wartosc"]);
                                        break;
                                    case "date":
                                        attribute.ValueDate = reader["Wartosc"].ToString();
                                        break;
                                    case "datetime":
                                        if (DateTime.TryParse(reader["Wartosc"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeValue))
                                        {
                                            attribute.ValueDate = dateTimeValue.ToString("yyyy-MM-dd"); // Extract date part
                                            attribute.ValueTime = dateTimeValue.ToString("HH:mm:ss");   // Extract time part
                                        }
                                        else
                                        {
                                            throw new Exception($"Invalid datetime format: {reader["Wartosc"]}");
                                        }
                                        break;
                                    case "decimal":
                                        attribute.ValueDecimal = Decimal.Parse(reader["Wartosc"].ToString(), CultureInfo.InvariantCulture);
                                        break;
                                    case "text":
                                        attribute.ValueText = reader["Wartosc"].ToString();
                                        break;
                                    default:
                                        throw new Exception($"Unknown type: {attribute.Type}");
                                }


                                attributes.Add(attribute);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogToTable(prodcuctId, productType, "DOCUMENT", 0, "Error while fetching document attributes from database. Error Message: " + ex.Message);
                int resultUpdate = await UpdateAttribute(prodcuctId, productType, "StatusWMS", "Błąd synchronizacji");
                if (resultUpdate != 0)
                {
                    _logger.Error("Error while updating attribute StatusWMS for document: " + resultUpdate.ToString());
                }

                _logger.Error(ex, "Error while fetching document attributes from database");
                throw new ProcessingException();
            }
            return attributes;
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            List<Product> products = new List<Product>();
            string procedureName = "kkur.WMSPobierzTowary";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (SqlCommand command = new SqlCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    using (SqlDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        List<Task> updateTasks = new List<Task>();

                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            int productId = Convert.ToInt32(reader["TwrNumer"]);
                            int productType = Convert.ToInt32(reader["TwrTyp"]);

                            updateTasks.Add(UpdateAttribute(productId, productType, "StatusWMS", "Przetwarzane"));

                            try
                            {
                                // Fetch product-related data in parallel
                                var imagesTask = GetProductImagesAsync(productId, productType);
                                var providersTask = GetProductProvidersAsync(productId, productType);
                                var unitsTask = GetProductUnitsAsync(productId, productType);
                                var attributesTask = GetProcuctAttributesAsync(productId, productType);

                                Product product = new Product()
                                {
                                    ErpId = productId,
                                    Name = reader["Nazwa"].ToString(),
                                    Source = "ERP",
                                    Symbol = reader["Kod"].ToString(),
                                    Unit = reader["Jm"].ToString(),
                                    Group = reader["Grupa"].ToString(),

                                    Images = await imagesTask.ConfigureAwait(false),
                                    Providers = await providersTask.ConfigureAwait(false),
                                    UnitsOfMeasure = await unitsTask.ConfigureAwait(false),
                                    Attributes = await attributesTask.ConfigureAwait(false)
                                };

                                products.Add(product);
                            }
                            catch (Exception ex)
                            {
                                await LogToTable(productId, productType, "ARTICLE", 0, "Error while fetching product from database. Error Message: " + ex.Message);
                                int resultUpdate = await UpdateAttribute(productId, productType, "StatusWMS", "Błąd synchronizacji");
                                if (resultUpdate != 0)
                                {
                                    _logger.Error("Error while updating attribute StatusWMS for product: " + resultUpdate.ToString());
                                }
                                _logger.Error(ex, "Error while fetching product from database");
                            }
                        }

                        await Task.WhenAll(updateTasks).ConfigureAwait(false);
                    }
                }
            }
            return products;
        }

        private async Task<List<Image>> GetProductImagesAsync(int prodcuctId, int productType)
        {
            List<Image> images = new List<Image>();
            try
            {
                string procedureName = "kkur.WMSPobierzZdjeciaTowaru";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(procedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("@GidNumer", SqlDbType.Int).Value = prodcuctId;
                        command.Parameters.Add("@GidTyp", SqlDbType.Int).Value = productType;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Image image = new Image()
                                {
                                    Default = Convert.ToBoolean((int)reader["Default"]),
                                    Path = reader["Path"].ToString(),
                                    CreatedDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                                };

                                images.Add(image);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogToTable(prodcuctId, productType, "ARTICLE", 0, "Error while fetching product images from database. Error Message: " + ex.Message);
                int resultUpdate = await UpdateAttribute(prodcuctId, productType, "StatusWMS", "Błąd synchronizacji");
                if (resultUpdate != 0)
                {
                    _logger.Error("Error while updating attribute StatusWMS for product: " + resultUpdate.ToString());
                }

                _logger.Error(ex, "Error while fetching product images from database");
                throw new ProcessingException();
            }
            return images;
        }

        private async Task<List<ProductProvider>> GetProductProvidersAsync(int prodcuctId, int productType)
        {
            List<ProductProvider> providers = new List<ProductProvider>();
            try
            {
                string procedureName = "kkur.WMSPobierzDostawcowTowaru";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(procedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("@GidNumer", SqlDbType.Int).Value = prodcuctId;
                        command.Parameters.Add("@GidTyp", SqlDbType.Int).Value = productType;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                ProductProvider provider = new ProductProvider()
                                {
                                    ContractorId = Convert.ToInt32(reader["KntNumer"]),
                                    ContractorSource = "ERP",
                                    Code = reader["Kod"].ToString(),
                                    Symbol = reader["Symbol"].ToString(),
                                    CreatedDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                                };

                                providers.Add(provider);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogToTable(prodcuctId, productType, "ARTICLE", 0, "Error while fetching product providers from database. Error Message: " + ex.Message);
                int resultUpdate = await UpdateAttribute(prodcuctId, productType, "StatusWMS", "Błąd synchronizacji");
                if (resultUpdate != 0)
                {
                    _logger.Error("Error while updating attribute StatusWMS for product: " + resultUpdate.ToString());
                }

                _logger.Error(ex, "Error while fetching product providers from database");
                throw new ProcessingException();
            }
            return providers;
        }

        private async Task<List<ProductUnit>> GetProductUnitsAsync(int prodcuctId, int productType)
        {
            List<ProductUnit> units = new List<ProductUnit>();
            try
            {
                string procedureName = "kkur.WMSPobierzJMTowaru";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(procedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("@GidNumer", SqlDbType.Int).Value = prodcuctId;
                        command.Parameters.Add("@GidTyp", SqlDbType.Int).Value = productType;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                ProductUnit unit = new ProductUnit()
                                {
                                    Default = Convert.ToBoolean((int)reader["Glowna"]),
                                    Unit = reader["Kod"].ToString(),
                                    Eans = new List<string> { reader["EAN"].ToString() },
                                    ConverterToMainUnit = Convert.ToInt32(reader["KonwersjaDoGlownej"]),
                                    Height = Convert.ToDecimal(reader["Wysokosc"]),
                                    Length = Convert.ToDecimal(reader["Dlugosc"]),
                                    Width = Convert.ToDecimal(reader["Szerokosc"]),
                                    Weight = Convert.ToDecimal(reader["Waga"])
                                };

                                units.Add(unit);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogToTable(prodcuctId, productType, "ARTICLE", 0, "Error while fetching product units from database. ERROR Message: " + ex.Message);
                int resultUpdate = await UpdateAttribute(prodcuctId, productType, "StatusWMS", "Błąd synchronizacji");
                if (resultUpdate != 0)
                {
                    _logger.Error("Error while updating attribute StatusWMS for product: " + resultUpdate.ToString());
                }

                _logger.Error(ex, "Error while fetching product units from database");
                throw new ProcessingException();
            }
            return units;
        }

        private async Task<List<Models.Attribute>> GetProcuctAttributesAsync(int prodcuctId, int productType)
        {
            List<Models.Attribute> attributes = new List<Models.Attribute>();
            try
            {
                string procedureName = "kkur.WMSPobierzAtrybutyTowaru";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(procedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("@GidNumer", SqlDbType.Int).Value = prodcuctId;
                        command.Parameters.Add("@GidTyp", SqlDbType.Int).Value = productType;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Models.Attribute attribute = new Models.Attribute()
                                {
                                    Symbol = reader["Klasa"].ToString(),
                                    Type = reader["Typ"].ToString(),
                                };

                                switch (attribute.Type.ToLower())
                                {
                                    case "integer":
                                        attribute.ValueInt = Convert.ToInt32(reader["Wartosc"]);
                                        break;
                                    case "date":
                                        attribute.ValueDate = reader["Wartosc"].ToString();
                                        break;
                                    case "datetime":
                                        if (DateTime.TryParse(reader["Wartosc"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeValue))
                                        {
                                            attribute.ValueDate = dateTimeValue.ToString("yyyy-MM-dd");
                                            attribute.ValueTime = dateTimeValue.ToString("HH:mm:ss");
                                        }
                                        else
                                        {
                                            throw new Exception($"Invalid datetime format: {reader["Wartosc"]}");
                                        }
                                        break;
                                    case "decimal":
                                        if (Decimal.TryParse(reader["Wartosc"].ToString(), NumberStyles.Any, new CultureInfo("en-US"), out Decimal value))
                                        {
                                            attribute.ValueDecimal = value;
                                        }
                                        else
                                        {
                                            throw new Exception($"Invalid decimal format: {reader["Wartosc"]}");
                                        }
                                        break;
                                    case "text":
                                        attribute.ValueText = reader["Wartosc"].ToString();
                                        break;
                                    default:
                                        throw new Exception($"Unknown type: {attribute.Type}");
                                }


                                attributes.Add(attribute);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogToTable(prodcuctId, productType, "ARTICLE", 0, "Error while fetching product units from database. ERROR Message: " + ex.Message);
                int resultUpdate = await UpdateAttribute(prodcuctId, productType, "StatusWMS", "Błąd synchronizacji");
                if (resultUpdate != 0)
                {
                    _logger.Error("Error while updating attribute StatusWMS for product: " + resultUpdate.ToString());
                }

                _logger.Error(ex, "Error while fetching product units from database");
                throw new ProcessingException();
            }
            return attributes;
        }
        public async Task<List<Client>> GetClientsAsync()
        {
            List<Client> clients = new List<Client>();
            string procedureName = "kkur.WMSPobierzKontrahentow";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (SqlCommand command = new SqlCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    using (SqlDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        List<Task> updateTasks = new List<Task>();

                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            int clientId = Convert.ToInt32(reader["KntNumer"]);
                            int clientType = Convert.ToInt32(reader["KntTyp"]);

                            try
                            {
                                updateTasks.Add(UpdateAttribute(clientId, clientType, "StatusWMS", "Przetwarzane"));

                                var attributesTask = GetClientAttributesAsync(clientId, clientType);
                                var addressesTask = GetClientAddressesAsync(clientId, clientType);

                                Client client = new Client()
                                {
                                    ErpId = clientId,
                                    Symbol = reader["Akronim"].ToString(),
                                    Name = reader["Nazwa"].ToString(),
                                    Source = "ERP",
                                    Email = reader["Email"].ToString(),
                                    Phone = reader["Telefon"].ToString(),
                                    IsSupplier = Convert.ToBoolean(Convert.ToInt32(reader["Dostawca"])),
                                    TaxNumber = reader["NIP"].ToString(),
                                    Description = reader["Opis"].ToString(),

                                    Address = new ClientAddress()
                                    {
                                        Active = true,
                                        ContractorId = clientId,
                                        Code = reader["Akronim"].ToString(),
                                        ContractorSource = "ERP",
                                        Name = reader["Nazwa"].ToString(),
                                        PostCity = reader["KodPocztowy"].ToString(),
                                        City = reader["Miasto"].ToString(),
                                        Street = reader["Ulica"].ToString(),
                                        Country = reader["Kraj"].ToString(),
                                    },

                                    // Await both tasks in parallel
                                    Attributes = await attributesTask.ConfigureAwait(false),
                                    Addresses = await addressesTask.ConfigureAwait(false),
                                };

                                clients.Add(client);
                            }
                            catch (Exception ex)
                            {
                                await LogToTable(clientId, clientType, "CONTRACTOR", 0, "Error while fetching clients from database. ErrorMessage: " + ex.Message);
                                int resultUpdate = await UpdateAttribute(clientId, clientType, "StatusWMS", "Błąd synchronizacji");
                                if (resultUpdate != 0)
                                {
                                    _logger.Error("Error while updating attribute StatusWMS for client: " + resultUpdate.ToString());
                                }
                                _logger.Error(ex, "Error while fetching client from database");
                            }
                        }

                        // Ensure all updates are completed before proceeding
                        await Task.WhenAll(updateTasks).ConfigureAwait(false);
                    }
                }
            }

            return clients;
        }

        private async Task<List<Models.Attribute>> GetClientAttributesAsync(int clientId, int clientType)
        {
            List<Models.Attribute> attributes = new List<Models.Attribute>();
            try
            {
                string procedureName = "kkur.WMSPobierzAtrybutyKontrahenta";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(procedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("@GidNumer", SqlDbType.Int).Value = clientId;
                        command.Parameters.Add("@GidTyp", SqlDbType.Int).Value = clientType;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Models.Attribute attribute = new Models.Attribute()
                                {
                                    Symbol = reader["Klasa"].ToString(),
                                    Type = reader["Typ"].ToString(),
                                };

                                switch (attribute.Type.ToLower())
                                {
                                    case "integer":
                                        attribute.ValueInt = Convert.ToInt32(reader["Wartosc"]);
                                        break;
                                    case "date":
                                        attribute.ValueDate = reader["Wartosc"].ToString();
                                        break;
                                    case "datetime":
                                        if (DateTime.TryParse(reader["Wartosc"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeValue))
                                        {
                                            attribute.ValueDate = dateTimeValue.ToString("yyyy-MM-dd"); // Extract date part
                                            attribute.ValueTime = dateTimeValue.ToString("HH:mm:ss");   // Extract time part
                                        }
                                        else
                                        {
                                            throw new Exception($"Invalid datetime format: {reader["Wartosc"]}");
                                        }
                                        break;
                                    case "decimal":
                                        if (Decimal.TryParse(reader["Wartosc"].ToString(), NumberStyles.Any, new CultureInfo("en-US"), out Decimal value))
                                        {
                                            attribute.ValueDecimal = value;
                                        }
                                        else
                                        {
                                            throw new Exception($"Invalid decimal format: {reader["Wartosc"]}");
                                        }
                                        break;
                                    case "text":
                                        attribute.ValueText = reader["Wartosc"].ToString();
                                        break;
                                    default:
                                        throw new Exception($"Unknown type: {attribute.Type}");
                                }


                                attributes.Add(attribute);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogToTable(clientId, clientType, "CONTRACTOR", 0, "Error while fetching client attributes from database. Eroor Message: " + ex.Message);
                int resultUpdate = await UpdateAttribute(clientId, clientType, "StatusWMS", "Błąd synchronizacji");
                if (resultUpdate != 0)
                {
                    _logger.Error("Error while updating attribute StatusWMS for client: " + resultUpdate.ToString());
                }

                _logger.Error(ex, "Error while fetching client attributes from database");
                throw new ProcessingException();
            }
            return attributes;
        }

        private async Task<List<ClientAddress>> GetClientAddressesAsync(int clientId, int clientType)
        {
            List<ClientAddress> addresses = new List<ClientAddress>();
            try
            {


                string procedureName = "kkur.WMSPobierzAdresyKontrahenta";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(procedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("@GidNumer", SqlDbType.Int).Value = clientId;
                        command.Parameters.Add("@GidTyp", SqlDbType.Int).Value = clientType;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                ClientAddress address = new ClientAddress()
                                {
                                    Active = true,
                                    ContractorId = Convert.ToInt32(reader["KntGidNumer"]),
                                    ContractorSource = "ERP",
                                    Code = reader["KodPocztowy"].ToString(),
                                    Name = reader["AdresNazwa"].ToString(),
                                    PostCity = reader["Miasto"].ToString(),
                                    City = reader["Miasto"].ToString(),
                                    Street = reader["Ulica"].ToString(),
                                    Country = reader["Kraj"].ToString(),
                                    DateFrom = reader["DataOd"].ToString()
                                };

                                addresses.Add(address);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogToTable(clientId, clientType, "CONTRACTOR", 0, "Error while fetching client addreses from database. Error Message: " + ex.Message);
                int resultUpdate = await UpdateAttribute(clientId, clientType, "StatusWMS", "Błąd synchronizacji");
                if (resultUpdate != 0)
                {
                    _logger.Error("Error while updating attribute StatusWMS for client: " + resultUpdate.ToString());
                }

                _logger.Error(ex, "Error while fetching client addreses from database");
                throw new ProcessingException();
            }
            return addresses;
        }

        public async Task<int> UpdateAttribute(int obiNumber, int obiType, string className, string value)
        {
            int result = 0;
            string procedureName = "kkur.ZaktualizujAtrybut";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("@ObjectId", SqlDbType.Int).Value = obiNumber;
                    command.Parameters.Add("@ObjectType", SqlDbType.Int).Value = obiType;
                    command.Parameters.Add("@ObjectLp", SqlDbType.Int).Value = 0;
                    command.Parameters.Add("@Class", SqlDbType.VarChar).Value = className;
                    command.Parameters.Add("@Value", SqlDbType.VarChar).Value = value;

                    result = await command.ExecuteNonQueryAsync();
                }
            }
            if (result > 0)
            {
                return 0;
            }

            result = _xlApiService.AddAttribute(obiNumber, obiType, className, value);

            return result;
        }

        public async Task<int> LogToTable(int id, int type, string endpoint, int success, string error = null)
        {
            int result = 0;
            string procedureName = "kkur.WMSZapiszLogDoTabeli";
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("@EntityId", SqlDbType.Int).Value = id;
                    command.Parameters.Add("@EntityType", SqlDbType.Int).Value = type;
                    command.Parameters.Add("@Success", SqlDbType.TinyInt).Value = success;
                    command.Parameters.Add("@Action", SqlDbType.VarChar).Value = endpoint;
                    command.Parameters.Add("@Error", SqlDbType.VarChar).Value = (object)error ?? DBNull.Value;

                    result = await command.ExecuteNonQueryAsync();
                }
            }

            return result;
        }
    }
}
