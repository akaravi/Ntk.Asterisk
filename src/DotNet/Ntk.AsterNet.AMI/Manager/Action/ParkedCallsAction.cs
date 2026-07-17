using System;
using Ntk.AsterNet.AMI.Manager.Event;

namespace Ntk.AsterNet.AMI.Manager.Action
{
    /// <summary>
    ///     The ParkedCallsAction requests a list of all currently parked calls.<br />
    ///     For each active channel a ParkedCallEvent is generated. After all parked
    ///     calls have been reported a ParkedCallsCompleteEvent is generated.
    /// </summary>
    /// <seealso cref="Ntk.AsterNet.AMI.Manager.Event.ParkedCallEvent" />
    /// <seealso cref="Ntk.AsterNet.AMI.Manager.Event.ParkedCallsCompleteEvent" />
    public class ParkedCallsAction : ManagerActionEvent
    {
        /// <summary> Get the name of this action, i.e. "ParkedCalls".</summary>
        public override string Action
        {
            get { return "ParkedCalls"; }
        }

        public override Type ActionCompleteEventClass()
        {
            return typeof (ParkedCallsCompleteEvent);
        }
    }
}