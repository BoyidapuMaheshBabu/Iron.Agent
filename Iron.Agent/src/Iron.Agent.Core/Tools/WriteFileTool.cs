namespace Iron.Agent.Core;

public class WriteFileTool : ITool
{
    public string Name => "write_file";

    public async Task<bool> ExecuteAsync(Step step, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = step.Parameters.GetValueOrDefault("path", "").ToString();
            var content = step.Parameters.GetValueOrDefault("content", "").ToString();
            await File.WriteAllTextAsync(path, content, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
