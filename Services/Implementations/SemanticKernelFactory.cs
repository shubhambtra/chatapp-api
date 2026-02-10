using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using ChatApp.API.Data;
using ChatApp.API.Plugins;

namespace ChatApp.API.Services.Implementations;

public interface ISemanticKernelFactory
{
    Task<Kernel> CreateKernelAsync();
    Task<ISemanticTextMemory> CreateMemoryAsync();
}

public class SemanticKernelFactory : ISemanticKernelFactory
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryStore _memoryStore;
    private readonly QueryRewritePlugin _queryRewritePlugin;
    private readonly KnowledgeSearchPlugin _knowledgeSearchPlugin;
    private readonly ResponseGenerationPlugin _responseGenerationPlugin;

    public SemanticKernelFactory(
        ApplicationDbContext context,
        IMemoryStore memoryStore,
        QueryRewritePlugin queryRewritePlugin,
        KnowledgeSearchPlugin knowledgeSearchPlugin,
        ResponseGenerationPlugin responseGenerationPlugin)
    {
        _context = context;
        _memoryStore = memoryStore;
        _queryRewritePlugin = queryRewritePlugin;
        _knowledgeSearchPlugin = knowledgeSearchPlugin;
        _responseGenerationPlugin = responseGenerationPlugin;
    }

    private async Task<(string ApiKey, string Model)> GetOpenAiSettingsAsync()
    {
        var settings = await _context.AppConfigurations.FirstOrDefaultAsync();
        var apiKey = settings?.OpenAiApiKey
            ?? throw new InvalidOperationException("OpenAI API key is not configured in AppConfigurations.");
        var model = settings?.OpenAiModel ?? "gpt-4o-mini";
        return (apiKey, model);
    }

    public async Task<Kernel> CreateKernelAsync()
    {
        var (apiKey, model) = await GetOpenAiSettingsAsync();

        var builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            modelId: model,
            apiKey: apiKey);

        builder.AddOpenAITextEmbeddingGeneration(
            modelId: "text-embedding-3-small",
            apiKey: apiKey);

        var kernel = builder.Build();

        // Import plugins
        kernel.ImportPluginFromObject(_queryRewritePlugin, "QueryRewrite");
        kernel.ImportPluginFromObject(_knowledgeSearchPlugin, "KnowledgeSearch");
        kernel.ImportPluginFromObject(_responseGenerationPlugin, "ResponseGeneration");

        return kernel;
    }

    public async Task<ISemanticTextMemory> CreateMemoryAsync()
    {
        var (apiKey, _) = await GetOpenAiSettingsAsync();

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0050
        var memory = new MemoryBuilder()
            .WithOpenAITextEmbeddingGeneration("text-embedding-3-small", apiKey)
            .WithMemoryStore(_memoryStore)
            .Build();
#pragma warning restore SKEXP0001, SKEXP0010, SKEXP0050

        return memory;
    }
}
