namespace Ntk.AsterNet.AMI.FastAGI.Scripts
{
	class AGINoAction : AGIScript
	{
		public override void Service(AGIRequest request, AGIChannel channel)
		{
			base.Hangup();
		}
	}
}
