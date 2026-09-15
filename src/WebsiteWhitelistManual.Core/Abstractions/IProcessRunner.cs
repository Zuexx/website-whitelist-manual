namespace WebsiteWhitelistManual.Core.Abstractions;

public interface IProcessRunner
{
    ProcessResult Run(string fileName, string arguments);
}
