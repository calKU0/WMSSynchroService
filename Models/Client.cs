using System.Collections.Generic;

namespace PinquarkWMSSynchro.Models
{
    public class Client
    {
        public ClientAddress Address { get; set; }
        public List<ClientAddress> Addresses { get; set; }
        public string Email { get; set; }
        public int ErpId { get; set; }
        public bool IsSupplier { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Source { get; set; }
        public string Symbol { get; set; }
        public string TaxNumber { get; set; }
    }
}
