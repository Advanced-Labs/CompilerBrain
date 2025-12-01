using CompilerBrain;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CompilerBrain;

public class CompilerBrainChatService
{
    ChatClientAgent agent;
    AgentThread thread;

    public CompilerBrainChatService(IChatClient chatClient)
    {
        this.agent = chatClient.CreateAIAgent(
           instructions: "You are C# expert.",
           name: "Main Agent");

        this.thread = agent.GetNewThread();
    }

    public async Task<AgentRunResponse> RunAsync(string message, CancellationToken cancellationToken)
    {
        return await agent.RunAsync(message, thread, cancellationToken: cancellationToken);
    }
}
