using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinquarkWMSSynchro.Models
{
    public class Product : Image
    {
        public int ErpId { get; set; }
        public string Ean { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
        public string Source { get; set; }
        public string Unit { get; set; }
        public List<Image> Images { get; set; }
        public List<ProductProvider> Providers { get; set; }
        public List<ProductUnit> UnitsOfMeasure { get; set; }
    }
}
