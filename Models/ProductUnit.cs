using System.Collections.Generic;

namespace PinquarkWMSSynchro.Models
{
    public class ProductUnit
    {
        public bool Default { get; set; }
        public string Unit { get; set; }
        public int ConverterToMainUnit { get; set; }
        public List<string> Eans { get; set; }
        public decimal Height { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }
        public decimal Weight { get; set; }
    }
}
