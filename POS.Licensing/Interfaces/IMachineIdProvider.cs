namespace POS.Licensing.Interfaces;

public interface IMachineIdProvider
{
    string GetMachineId();
    bool ValidateMachineId(string expectedMachineId);
}
