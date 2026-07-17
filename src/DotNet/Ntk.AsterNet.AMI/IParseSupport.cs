using System.Collections.Generic;

namespace Ntk.AsterNet.AMI
{
    internal interface IParseSupport
    {
        Dictionary<string, string> Attributes { get; }
        bool Parse(string key, string value);
        Dictionary<string, string> ParseSpecial(Dictionary<string, string> attributes);
    }
}