using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace ChatApp.API.Plugins;

/// <summary>
/// Generates structured RAG responses from customer messages and knowledge context.
/// Uses the same strict system prompt as the original implementation.
/// </summary>
public class ResponseGenerationPlugin
{
    private const string SystemPrompt = @"You are a customer support assistant with STRICT limitations. You can ONLY answer questions using the EXACT information provided in the knowledge base content below.

CRITICAL RULES - YOU MUST FOLLOW THESE:
1. ONLY use information that is EXPLICITLY stated in the provided knowledge base content
2. If the customer's question is NOT DIRECTLY answered by the knowledge base content, you MUST set suggested_reply to: 'I don't have specific information about that topic. A support agent will be with you shortly to help.'
3. NEVER use your general knowledge or training data to answer questions
4. NEVER make assumptions or inferences beyond what is explicitly written in the knowledge base
5. If the question is about a completely different topic than what's in the knowledge base (e.g., asking about cooking when KB is about software), always decline to answer
6. Be honest - if you're not sure if the KB answers the question, decline to answer

EXAMPLES OF WHEN TO DECLINE:
- Customer asks 'how to make tea' but KB is about product features → DECLINE
- Customer asks about pricing but KB only has technical docs → DECLINE
- Customer asks something vaguely related but KB doesn't have the specific answer → DECLINE";

    [KernelFunction("GenerateRagResponse")]
    [Description("Generates a structured JSON response from customer message and knowledge base context")]
    public async Task<string> GenerateRagResponseAsync(
        Kernel kernel,
        [Description("The original customer message")] string customerMessage,
        [Description("JSON array of knowledge base search results")] string knowledgeContext)
    {
        var prompt = @$"{{{{$system_prompt}}}}

Analyze this customer message and respond ONLY if the knowledge base below contains a DIRECT answer.

KNOWLEDGE BASE (You MUST only use this information to answer):
{{{{$knowledge_context}}}}

Customer message: ""{{{{$customer_message}}}}""

IMPORTANT: First check if the knowledge base content above DIRECTLY answers this question. If NOT, set suggested_reply to the decline message.

Return ONLY valid JSON:
{{{{{{
  ""suggested_reply"": ""<answer from KB OR decline message if KB doesn't cover this topic>"",
  ""interest_level"": ""Low | Medium | High"",
  ""conversion_percentage"": <number 0-100>,
  ""objection"": ""<any objection detected or empty string>"",
  ""next_action"": ""<recommended action>"",
  ""sentiment"": ""positive | negative | neutral"",
  ""intent"": ""<customer intent>"",
  ""keywords"": [""<relevant keywords>""]
}}}}}}";

        var function = KernelFunctionFactory.CreateFromPrompt(prompt);
        var result = await kernel.InvokeAsync(function, new KernelArguments
        {
            ["system_prompt"] = SystemPrompt,
            ["knowledge_context"] = knowledgeContext,
            ["customer_message"] = customerMessage
        });

        var content = result.GetValue<string>()?.Trim() ?? "{}";

        // Strip markdown code blocks if present
        if (content.StartsWith("```json"))
            content = content[7..];
        else if (content.StartsWith("```"))
            content = content[3..];
        if (content.EndsWith("```"))
            content = content[..^3];

        return content.Trim();
    }
}
