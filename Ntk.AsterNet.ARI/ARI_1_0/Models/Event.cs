/*
	AsterNET ARI Framework
	Automatically generated file @ 9/22/2016 4:43:49 PM
*/
using System;
using System.Collections.Generic;
using Ntk.AsterNet.ARI.Actions;

namespace Ntk.AsterNet.ARI.Models
{
    /// <summary>
    /// Base type for asynchronous events from Asterisk.
    /// </summary>
    public class Event : Message
    {


        /// <summary>
        /// Name of the application receiving the event.
        /// </summary>
        public string Application { get; set; }

        /// <summary>
        /// Time at which this event was created.
        /// </summary>
        public DateTime Timestamp { get; set; }

    }
}
