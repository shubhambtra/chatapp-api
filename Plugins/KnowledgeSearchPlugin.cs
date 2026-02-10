using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;

namespace ChatApp.API.Plugins;

/// <summary>
/// Wraps SK memory search as a kernel function for knowledge base retrieval.
/// </summary>
public class KnowledgeSearchPlugin
{
    [KernelFunction("SearchKnowledgeBase")]
    [Description("Searches the knowledge base for relevant content using semantic similarity")]
    public async Task<string> SearchKnowledgeBaseAsync(
        ISemanticTextMemory memory,
        [Description("The search query")] string query,
        [Description("The site ID (collection)")] string siteId,
        int maxResults = 5,
        double minSimilarity = 0.7)
    {
        var results = new List<object>();

        await foreach (var result in memory.SearchAsync(
            collection: siteId,
            query: query,
            limit: maxResults,
            minRelevanceScore: minSimilarity))
        {
            results.Add(new
            {
                chunkId = result.Metadata.Id,
                documentId = result.Metadata.AdditionalMetadata,
                documentTitle = result.Metadata.Description,
                content = result.Metadata.Text,
                similarity = result.Relevance
            });
        }

        return JsonSerializer.Serialize(results);
    }
}
