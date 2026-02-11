using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatApp.API.Plugins;

/// <summary>
/// Generates RAG responses using direct chat completion (avoids SK template brace issues).
/// </summary>
public class ResponseGenerationPlugin
{
    private const string SystemPrompt = @"You are a customer support assistant. You can ONLY answer using the knowledge base content provided.

RULES:
1. ONLY use information EXPLICITLY stated in the provided knowledge base content.
2. If the question is NOT answered by the knowledge base, reply: ""I don't have specific information about that in our knowledge base.""
3. NEVER use your general knowledge. NEVER make assumptions beyond what is in the knowledge base.
4. Keep answers concise, helpful, and friendly.
5. Do NOT wrap your answer in JSON or any special format — just reply naturally.";

    [KernelFunction("GenerateRagResponse")]
    [Description("Generates a plain-text answer from customer message and knowledge base context")]
    public async Task<string> GenerateRagResponseAsync(
        Kernel kernel,
        [Description("The original customer message")] string customerMessage,
        [Description("JSON array of knowledge base search results")] string knowledgeContext)
    {
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(SystemPrompt);
        chatHistory.AddUserMessage(
            $"KNOWLEDGE BASE CONTENT:\n{knowledgeContext}\n\nCUSTOMER QUESTION: {customerMessage}");

        var response = await chatService.GetChatMessageContentAsync(chatHistory);

        return response.Content?.Trim() ?? "I don't have specific information about that in our knowledge base.";
    }
}
