using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.Core.Tests.Fakes;

public sealed record RecordedInvocation(string FileName, IReadOnlyList<string> Arguments);

public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly ProcessResult _result;

    public FakeProcessRunner(ProcessResult result)
    {
        _result = result;
    }

    public List<RecordedInvocation> Invocations { get; } = new();

    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        Invocations.Add(new RecordedInvocation(fileName, arguments));
        return _result;
    }
}
