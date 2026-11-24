namespace Iron.Agent.Core;

public interface ITool
{
    string Name { get; }
    Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default);
}
