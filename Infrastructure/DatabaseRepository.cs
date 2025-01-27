using Microsoft.Data.SqlClient;
using PinquarkWMSSynchro.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PinquarkWMSSynchro
{
    public class DatabaseRepository
    {
        private readonly string _connectionString;
        public DatabaseRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<Document>> GetDocumentsAsync()
        {
            List<Document> docs = new List<Document>();
            string procedureName = "kkur.WMSPobierzDokumenty";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Document document = new Document()
                            {
                                ErpId = Convert.ToInt32(reader["TrnNumer"]),
                                DocumentType = reader["DokumentTyp"].ToString(),
                                ErpCode = reader["PelnaNazwa"].ToString(),
                                ErpStatusSymbol = reader["Status"].ToString(),
                                Source = "ERP",
                                Symbol = reader["Kod"].ToString(),
                                Date = reader["Data"].ToString(),
                                Note = reader["Opis"].ToString(),
                                DeliveryMethodSymbol = reader["SposobDostawy"].ToString(),
                                Priority = Convert.ToInt32(reader["Priorytet"]),
                                WarehouseSymbol = reader["Magazyn"].ToString(),
                                ReciepentId = Convert.ToInt32(reader["GidNumerOdbiorcy"]),
                                ReciepentSource = "ERP",

                                Contractor = new DocumentClient()
                                {
                                    ErpId = Convert.ToInt32(reader["KntNumer"]),
                                    Source = "ERP"
                                },

                                deliveryAddress = new ClientAddress()
                                {
                                    Active = true,
                                    ContractorId = Convert.ToInt32(reader["KntNumer"]),
                                    Code = reader["KodPocztowy"].ToString(),
                                    ContractorSource = "ERP",
                                    Name = reader["AdresNazwa"].ToString(),
                                    PostCity = reader["Miasto"].ToString(),
                                    City = reader["Miasto"].ToString(),
                                    Street = reader["Ulica"].ToString(),
                                    Country = reader["Kraj"].ToString(),
                                    DateFrom = reader["DataOd"].ToString(),
                                },

                                Positions = await GetDocumentElementsAsync(Convert.ToInt32(reader["TrnNumer"]), Convert.ToInt32(reader["TrnTyp"])),
                            };

                            docs.Add(document);
                        }
                    }
                }
            }

            return docs;
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
                                No = 0,
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
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Product product = new Product()
                            {
                                ErpId = Convert.ToInt32(reader["TwrNumer"]),
                                Name = reader["Nazwa"].ToString(),
                                Source = "ERP",
                                Symbol = reader["Kod"].ToString(),
                                Unit = reader["Jm"].ToString(),
                                Group = reader["Grupa"].ToString(),
                                Images = await GetProductImagesAsync(Convert.ToInt32(reader["TwrNumer"]), Convert.ToInt32(reader["TwrTyp"])),
                                Providers = await GetProductProvidersAsync(Convert.ToInt32(reader["TwrNumer"]), Convert.ToInt32(reader["TwrTyp"])),
                                UnitsOfMeasure = await GetProductUnitsAsync(Convert.ToInt32(reader["TwrNumer"]), Convert.ToInt32(reader["TwrTyp"])),

                            };

                            products.Add(product);
                        }
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
                                Height = Convert.ToInt32(reader["Wysokosc"]),
                                Length = Convert.ToInt32(reader["Dlugosc"]),
                                Width = Convert.ToInt32(reader["Szerokosc"]),
                                Weight = Convert.ToInt32(reader["Waga"])
                            };

                            units.Add(unit);
                        }
                    }
                }
            }
            return units;
        }

        public async Task<List<Client>> GetClientsAsync()
        {
            List<Client> clients = new List<Client>();
            string procedureName = "kkur.WMSPobierzKontrahentow";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Client client = new Client()
                            {
                                ErpId = Convert.ToInt32(reader["KntNumer"]),
                                Symbol = reader["Akronim"].ToString(),
                                Name = reader["Nazwa"].ToString(),
                                Source = "ERP",
                                Email = reader["Email"].ToString(),
                                Phone = reader["Telefon"].ToString(),
                                IsSupplier = Convert.ToBoolean((int)reader["Dostawca"]),
                                TaxNumber = reader["NIP"].ToString(),

                                Address = new ClientAddress()
                                {
                                    Active = true,
                                    ContractorId = Convert.ToInt32(reader["KntNumer"]),
                                    Code = reader["Akronim"].ToString(),
                                    ContractorSource = "ERP",
                                    Name = reader["Nazwa"].ToString(),
                                    PostCity = reader["KodPocztowy"].ToString(),
                                    City = reader["Miasto"].ToString(),
                                    Street = reader["Ulica"].ToString(),
                                    Country = reader["Kraj"].ToString(),
                                },

                                //Addresses = await GetClientAddressesAsync(Convert.ToInt32(reader["KntNumer"]), Convert.ToInt32(reader["KntTyp"])),
                            };

                            clients.Add(client);
                        }
                    }
                }
            }

            return clients;
        }

        private async Task<List<ClientAddress>> GetClientAddressesAsync(int clientId, int clientType)
        {
            List<ClientAddress> addresses = new List<ClientAddress>();
            string procedureName = "kkur.WMSPobierzAdresyKontrahentow";

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

    }
}
