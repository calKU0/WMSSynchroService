using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinquarkWMSSynchro.Models
{
    public class Feedback
    {
        public string Action { get; set; } // Enum: DELETE, SAVE
        public string Entity { get; set; } // Enum: ARTICLE, ARTICLE_BATCH, CONTRACTOR, DOCUMENT, DOCUMENTS_WRAPPER, POSITION
        public Dictionary<string, string> Errors { get; set; }
        public string Id { get; set; }
        public Dictionary<string, string> ResponseMessages { get; set; }
        public bool Success { get; set; }
    }
}
