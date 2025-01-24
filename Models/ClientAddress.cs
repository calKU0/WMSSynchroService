using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinquarkWMSSynchro.Models
{
    public class ClientAddress
    {
        public bool Active { get; set; }
        public string City { get; set; }
        public string Code { get; set; }
        public int ContractorId { get; set; }
        public string ContractorSource { get; set; }
        public string Country { get; set; }
        public string Name { get; set; }
        public string PostCity { get; set; }
        public string Street { get; set; }
    }
}
