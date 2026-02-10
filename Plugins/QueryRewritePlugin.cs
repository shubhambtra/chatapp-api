using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace ChatApp.API.Plugins;

/// <summary>
/// Rewrites user queries for better knowledge base retrieval.
/// Fixes typos, expands abbreviations, and optimizes for semantic search.
/// </summary>
public class QueryRewritePlugin
{
    [KernelFunction("RewriteQuery")]
    [Description("Rewrites a user query to optimize it for knowledge base search")]
    public async Task<string> RewriteQueryAsync(
        Kernel kernel,
        [Description("The raw user query")] string query)
    {
        var prompt = @"You are a query rewriting assistant. Your job is to rewrite a customer support query to optimize it for semantic search against a knowledge base.

Rules:
- Fix any typos or grammatical errors
- Expand abbreviations (e.g., 'pw' → 'password', 'acct' → 'account')
- Remove filler words and conversational fluff
- Keep the core intent and key terms
- Output ONLY the rewritten query, nothing else
- If the query is already clear and well-formed, return it as-is

User query: {{$input}}

Rewritten query:";

        var function = KernelFunctionFactory.CreateFromPrompt(prompt);
        var result = await kernel.InvokeAsync(function, new KernelArguments { ["input"] = query });
        var rewritten = result.GetValue<string>()?.Trim();

        // Fallback to original if rewrite fails or is empty
        return string.IsNullOrWhiteSpace(rewritten) ? query : rewritten;
    }
}
