using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BestPriceEngine.Models
{
    public class PriceEvent
    {
        public int ProductId { get; set; }

        public int ListingId { get; set; }

        public EventTypeEnum EventType { get; set; }

        public double Price { get; set; }

        public DateTime CreateDate { get; set; }
    }
}
