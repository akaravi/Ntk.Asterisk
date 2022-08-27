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
    /// Event showing the completion of a media playback operation.
    /// </summary>
    public class PlaybackFinishedEvent : Event
    {


        /// <summary>
        /// Playback control object
        /// </summary>
        public Playback Playback { get; set; }

    }
}
