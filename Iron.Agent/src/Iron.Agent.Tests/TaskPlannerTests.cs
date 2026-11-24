namespace Iron.Agent.Tests;

using Iron.Agent.Core;
using Xunit;

public class TaskPlannerTests
{
    private class StubOllamaClient : IOllamaClient
    {
        private readonly string _chatResponse;

        public StubOllamaClient(string chatResponse)
        {
            _chatResponse = chatResponse;
        }

        public Task<string> GenerateAsync(string prompt, string model, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);

        public IAsyncEnumerable<string> GenerateStreamAsync(string prompt, string model, CancellationToken cancellationToken = default) => EmptyStream();

        public Task<string> ChatAsync(string systemPrompt, string userMessage, string model, CancellationToken cancellationToken = default) => Task.FromResult(_chatResponse);

        public Task<bool> IsModelAvailableAsync(string model, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<string[]> ListModelsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<string>());

        static IAsyncEnumerable<string> EmptyStream() => EmptyImpl();
        static async IAsyncEnumerable<string> EmptyImpl()
        {
            yield break;
        }
    }

    private const string PlanJson = """
        {
          "steps": [
            {
              "order": 1,
              "tool": "read_file",
              "description": "Read the config file",
              "parameters": { "path": "config.txt" },
              "verification": "File contents were read",
              "action_class": "Read"
            },
            {
              "order": 2,
              "tool": "write_file",
              "description": "Write the output file",
              "parameters": { "path": "output.txt", "content": "result" },
              "verification": "File was written",
              "action_class": "Write"
            }
          ]
        }
        """;

    [Fact]
    public async Task PlanAsync_ParsesJsonResponseIntoTaskPlan()
    {
        var planner = new TaskPlanner(new StubOllamaClient(PlanJson));
        var plan = await planner.PlanAsync("Read config and write output");

        Assert.NotNull(plan);
        Assert.Equal("Read config and write output", plan.UserInstruction);
        Assert.Equal(TaskStatus.Pending, plan.Status);
        Assert.Equal(2, plan.Steps.Length);

        Assert.Equal(1, plan.Steps[0].Order);
        Assert.Equal("read_file", plan.Steps[0].Tool);
        Assert.Equal("Read the config file", plan.Steps[0].Description);
        Assert.Equal(ActionClass.Read, plan.Steps[0].ActionClass);
        Assert.Equal("config.txt", plan.Steps[0].Parameters["path"].ToString());

        Assert.Equal(2, plan.Steps[1].Order);
        Assert.Equal("write_file", plan.Steps[1].Tool);
        Assert.Equal("Write the output file", plan.Steps[1].Description);
        Assert.Equal(ActionClass.Write, plan.Steps[1].ActionClass);
        Assert.Equal("output.txt", plan.Steps[1].Parameters["path"].ToString());
        Assert.Equal("result", plan.Steps[1].Parameters["content"].ToString());
    }
}
