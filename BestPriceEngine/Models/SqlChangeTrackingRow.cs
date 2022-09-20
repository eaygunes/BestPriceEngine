using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BestPriceEngine.Models
{
    public class SqlChangeTrackingRow
    {
        public int ProductId { get; set; }
        public int ListingId { get; set; }
        public double Price { get; set; }
        public int IsActive { get; set; }
        public int IsDeleted { get; set; }
        public char ChangeTrackingOperation { get; set; }

    }
}
