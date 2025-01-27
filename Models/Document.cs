using System.Collections.Generic;

namespace PinquarkWMSSynchro.Models
{
    public class Document
    {
        public int ErpId { get; set; }
        public string DocumentType { get; set; }
        public string ErpCode { get; set; }
        public string ErpStatusSymbol { get; set; }
        public string Source { get; set; }
        public string Symbol { get; set; }
        public string Date { get; set; }
        public string Note { get; set; }
        public string DeliveryMethodSymbol { get; set; }
        public int Priority { get; set; }
        public string WarehouseSymbol { get; set; }
        public int ReciepentId { get; set; }
        public string ReciepentSource { get; set; }
        public DocumentClient Contractor { get; set; }
        public ClientAddress deliveryAddress { get; set; }
        public List<DocumentPosition> Positions { get; set; }
    }
}
