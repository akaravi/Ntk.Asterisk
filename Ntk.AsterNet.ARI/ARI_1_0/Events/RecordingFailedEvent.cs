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
    /// Event showing failure of a recording operation.
    /// </summary>
    public class RecordingFailedEvent : Event
    {


        /// <summary>
        /// Recording control object
        /// </summary>
        public LiveRecording Recording { get; set; }

    }
}
