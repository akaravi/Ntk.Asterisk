using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Response;

namespace Ntk.AsterNet.AMI.Manager
{
    /// <summary>
    ///     An Interface to handle responses received from an asterisk server.
    /// </summary>
    /// <seealso cref="ManagerResponse" />
    public interface IResponseHandler
    {
        ManagerAction Action { get; }

        int Hash { get; set; }

        /// <summary>
        ///     This method is called when a response is received.
        /// </summary>
        /// <param name="response">the response received</param>
        void HandleResponse(ManagerResponse response);

        void Free();
    }
}