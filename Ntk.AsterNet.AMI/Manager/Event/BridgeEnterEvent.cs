﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ntk.AsterNet.AMI.Manager.Event
{
    public class BridgeEnterEvent : BridgeActivityEvent
    {
        public BridgeEnterEvent(ManagerConnection source) : base(source)
        {
        }
    }
}
