using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public List<DocumentElement> Positions { get; set; }
    }
}
