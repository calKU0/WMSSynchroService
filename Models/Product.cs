using System.Collections.Generic;

namespace PinquarkWMSSynchro.Models
{
    public class Product
    {
        public int ErpId { get; set; }
        public string Ean { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
        public string Source { get; set; }
        public string Unit { get; set; }
        public string Group { get; set; }
        public List<Image> Images { get; set; }
        public List<ProductProvider> Providers { get; set; }
        public List<ProductUnit> UnitsOfMeasure { get; set; }
    }
}
