namespace Iron.Agent.Core;

public class ReadFileTool : ITool
{
    public string Name => "read_file";

    public Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = step.Parameters.GetValueOrDefault("path", "").ToString();
            File.ReadAllText(path);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
