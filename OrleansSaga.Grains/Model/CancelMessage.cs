using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Model
{
    public class CancelMessage
    {        
        public string Reason { get; private set; }

        public CancelMessage(string reason)
        {
            Reason = reason;
        }
    }

    public class SagaCanceled
    {

    }
}
