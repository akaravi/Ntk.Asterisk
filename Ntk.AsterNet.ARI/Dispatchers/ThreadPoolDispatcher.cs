using System;
using System.Threading;

namespace Ntk.AsterNet.ARI.Dispatchers
{
    sealed class ThreadPoolDispatcher : IAriDispatcher
    {
        public void Dispose()
        {
        }

        public void QueueAction(Action action)
        {
            ThreadPool.QueueUserWorkItem(_ => action());
        }
    }
}
