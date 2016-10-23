using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Model
{
    public class TicketToolState
    {
        public string TicketId { get; set; }

        public string WorkItemId { get; set; }

        public string ExternalSystemId { get; set; }

        public string Parameter { get; set; }
    }
}
