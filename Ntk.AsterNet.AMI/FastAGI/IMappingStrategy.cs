namespace Ntk.AsterNet.AMI.FastAGI
{
    public interface IMappingStrategy
    {
        AGIScript DetermineScript(AGIRequest request);
        void Load();
    }
}