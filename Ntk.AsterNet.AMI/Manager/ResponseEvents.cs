using System.Collections;
using System.Collections.Generic;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.AsterNet.AMI.Manager.Response;

namespace Ntk.AsterNet.AMI.Manager
{
    /// <summary>
    ///     Collection of ResponseEvent. Use in events generation actions.
    /// </summary>
    public class ResponseEvents
    {
        private readonly List<ResponseEvent> events;

        /// <summary>
        ///     Creates a new <see cref="ResponseEvents"/>.
        /// </summary>
        public ResponseEvents()
        {
            events = new List<ResponseEvent>();
            Complete = false;
        }

        /// <summary>
        ///     Gets or sets the response.
        /// </summary>
        public ManagerResponse Response { get; set; }

        /// <summary>
        ///     Gets the list of events.
        /// </summary>
        public List<ResponseEvent> Events
        {
            get { return events; }
        }

        /// <summary>
        ///     Indicates if all events have been received.
        /// </summary>
        public bool Complete { get; set; }

        /// <summary>
        ///     Adds a ResponseEvent that has been received.
        /// </summary>
        /// <param name="e"><see cref="ResponseEvent"/></param>
        public void AddEvent(ResponseEvent e)
        {
            lock (((IList) events).SyncRoot)
            {
                events.Add(e);
            }
        }
    }
}