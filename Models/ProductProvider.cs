using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinquarkWMSSynchro.Models
{
    public class ProductProvider
    {
        public int ContractorId { get; set; }
        public string ContractorSource { get; set; }
        public string Code { get; set; }
        public string Symbol { get; set; }
        public string CreatedDate { get; set; }
    }
}
