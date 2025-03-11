using Microsoft.Data.SqlClient;
using PinquarkWMSSynchro.Infrastructure;
using PinquarkWMSSynchro.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PinquarkWMSSynchro
{
    public class DatabaseRepository
    {
        private readonly string _connectionString;
        private readonly XlApiService _xlApiService;
        public DatabaseRepository(string connectionString, XlApiService xlApiService)
        {
            _connectionString = connectionString;
            _xlApiService = xlApiService;
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

                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            int trnNumer = Convert.ToInt32(reader["TrnNumer"]);
                            int trnTyp = Convert.ToInt32(reader["TrnTyp"]);

                            updateTasks.Add(UpdateAttribute(trnNumer, trnTyp, "StatusWMS", "Przetwarzane"));
                            positionTasks.Add(GetDocumentElementsAsync(trnNumer, trnTyp));

                            Document document = new Document()
                            {
                                ErpId = trnNumer,
                                ErpIdTxt = reader["TrnTyp"].ToString() + "|" + reader["TrnNumer"].ToString(),
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

                        await Task.WhenAll(updateTasks).ConfigureAwait(false);
                        var positionResults = await Task.WhenAll(positionTasks).ConfigureAwait(false);
                        for (int i = 0; i < documents.Count; i++)
                        {
                            documents[i].Positions = positionResults[i];
                        }
                    }
                }
            }

            return documents;
        }

        public async Task<List<DocumentPosition>> GetDocumentElementsAsync(int documentId, int documentType)
        {
            List<DocumentPosition> positions = new List<DocumentPosition>();
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

                return positions;
            }
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

                        await Task.WhenAll(updateTasks).ConfigureAwait(false);
                    }
                }
            }
            return products;
        }

        private async Task<List<Image>> GetProductImagesAsync(int prodcuctId, int productType)
        {
            List<Image> images = new List<Image>();
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
                return images;
            }
        }

        private async Task<List<ProductProvider>> GetProductProvidersAsync(int prodcuctId, int productType)
        {
            List<ProductProvider> providers = new List<ProductProvider>();
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
            return providers;
        }

        private async Task<List<ProductUnit>> GetProductUnitsAsync(int prodcuctId, int productType)
        {
            List<ProductUnit> units = new List<ProductUnit>();
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
            return units;
        }

        private async Task<List<Models.Attribute>> GetProcuctAttributesAsync(int prodcuctId, int productType)
        {
            List<Models.Attribute> attributes = new List<Models.Attribute>();
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
                                case "date":
                                    attribute.ValueDate = reader["Wartosc"].ToString();
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
                                case "date":
                                    attribute.ValueDate = reader["Wartosc"].ToString();
                                    break;
                                case "decimal":
                                    attribute.ValueDecimal = reader["Wartosc"] != DBNull.Value ? Convert.ToDecimal(reader["Wartosc"]) : 0;
                                    break;
                                case "int":
                                    attribute.ValueInt = reader["Wartosc"].ToString();
                                    break;
                                case "text":
                                    attribute.ValueText = reader["Wartosc"].ToString();
                                    break;
                                case "time":
                                    attribute.ValueTime = reader["Wartosc"].ToString();
                                    break;
                                default:
                                    throw new Exception($"Unknown type: {attribute.Type}");
                            }


                            attributes.Add(attribute);
                        }
                    }
                }
            }
            return attributes;
        }

        private async Task<List<ClientAddress>> GetClientAddressesAsync(int clientId, int clientType)
        {
            List<ClientAddress> addresses = new List<ClientAddress>();
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
