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
    /// The state of a peer associated with an endpoint has changed.
    /// </summary>
    public class PeerStatusChangeEvent : Event
    {


        /// <summary>
        /// no description provided
        /// </summary>
        public Endpoint Endpoint { get; set; }

        /// <summary>
        /// no description provided
        /// </summary>
        public Peer Peer { get; set; }

    }
}
