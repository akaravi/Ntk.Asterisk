using System;

namespace Ntk.AsterNet.AMI.Manager.Event
{
	public class MonitorStopEvent : ManagerEvent
	{
		#region Constructor - MonitorStop(ManagerConnection source)
		public MonitorStopEvent(ManagerConnection source)
			: base(source)
		{
		}
		#endregion
	}
}
