using System;
namespace Ntk.AsterNet.AMI.FastAGI.Command
{
	/// <summary>
	/// Does nothing.
	/// </summary>
	public class NoopCommand : AGICommand
	{
		/// <summary>
		/// Creates a new NoopCommand.
		/// </summary>
		public NoopCommand()
		{
		}
		public override string BuildCommand()
		{
			return "NOOP";
		}
	}
}