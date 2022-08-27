using System;

namespace Ntk.AsterNet.ARI
{
    interface IAriDispatcher : IDisposable
    {
        void QueueAction(Action action);
    }
}
