using System.Collections.Generic;

namespace Ntk.AsterNet.AMI.Manager
{
    /// <summary>
    ///     IActionVariable
    /// </summary>
    interface IActionVariable
    {
        Dictionary<string, string> GetVariables();
        void SetVariables(Dictionary<string, string> vars);
    }
}
