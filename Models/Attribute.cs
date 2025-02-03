using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinquarkWMSSynchro.Models
{
    public class Attribute
    {
        public string Symbol { get; set; }
        public string Type { get; set; }
        public string ValueDate { get; set; }
        public string ValueDateTo { get; set; }
        public decimal ValueDecimal { get; set; }
        public string ValueInt { get; set; }
        public string ValueText { get; set; }
        public string ValueTime { get; set; }
    }
}
