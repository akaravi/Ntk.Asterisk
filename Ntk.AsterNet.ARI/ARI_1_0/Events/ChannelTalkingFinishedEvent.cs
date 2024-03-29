﻿/*
	AsterNET ARI Framework
	Automatically generated file @ 9/22/2016 4:43:49 PM
*/
using System;
using System.Collections.Generic;
using Ntk.AsterNet.ARI.Actions;

namespace Ntk.AsterNet.ARI.Models
{
    /// <summary>
    /// Talking is no longer detected on the channel.
    /// </summary>
    public class ChannelTalkingFinishedEvent : Event
    {


        /// <summary>
        /// The channel on which talking completed.
        /// </summary>
        public Channel Channel { get; set; }

        /// <summary>
        /// The length of time, in milliseconds, that talking was detected on the channel
        /// </summary>
        public int Duration { get; set; }

    }
}
