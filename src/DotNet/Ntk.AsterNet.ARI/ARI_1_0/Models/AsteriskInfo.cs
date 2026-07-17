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
    /// Asterisk system information
    /// </summary>
    public class AsteriskInfo
    {


        /// <summary>
        /// Info about how Asterisk was built
        /// </summary>
        public BuildInfo Build { get; set; }

        /// <summary>
        /// Info about the system running Asterisk
        /// </summary>
        public SystemInfo System { get; set; }

        /// <summary>
        /// Info about Asterisk configuration
        /// </summary>
        public ConfigInfo Config { get; set; }

        /// <summary>
        /// Info about Asterisk status
        /// </summary>
        public StatusInfo Status { get; set; }

    }
}
