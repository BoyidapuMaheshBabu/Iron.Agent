namespace Iron.Agent.Core;

public class AgentException : Exception
{
    public AgentException(string message) : base(message) { }
    public AgentException(string message, Exception inner) : base(message, inner) { }
}

public class OllamaException : AgentException
{
    public OllamaException(string message) : base(message) { }
    public OllamaException(string message, Exception inner) : base(message, inner) { }
}

public class AutomationException : AgentException
{
    public AutomationException(string message) : base(message) { }
    public AutomationException(string message, Exception inner) : base(message, inner) { }
}

public class PlanningException : AgentException
{
    public PlanningException(string message) : base(message) { }
    public PlanningException(string message, Exception inner) : base(message, inner) { }
}

public class PermissionDeniedException : AgentException
{
    public PermissionDeniedException(string message) : base(message) { }
    public PermissionDeniedException(string message, Exception inner) : base(message, inner) { }
}