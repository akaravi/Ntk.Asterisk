using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ntk.AsterNet.AMI.Manager.Event
{
    public class ConfbridgeStartEvent : AbstractConfbridgeEvent
    {
        public ConfbridgeStartEvent(ManagerConnection source)
			: base(source)
		{
		}
    }
}
