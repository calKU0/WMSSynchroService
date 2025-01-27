namespace PinquarkWMSSynchro.Models
{
    public class DocumentPosition
    {
        public int ErpId { get; set; }
        public int Quantity { get; set; }
        public int No { get; set; }
        public string StatusSymbol { get; set; }
        public string Note { get; set; }
        public DocumentElement Article { get; set; }
    }
}
