using System.Collections.Generic;

namespace PinquarkWMSSynchro.Models
{
    public class ProductUnit
    {
        public bool Default { get; set; }
        public string Unit { get; set; }
        public int ConverterToMainUnit { get; set; }
        public List<string> Eans { get; set; }
        public int Height { get; set; }
        public int Length { get; set; }
        public int Width { get; set; }
        public int Weight { get; set; }
    }
}
