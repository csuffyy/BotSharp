using BotSharp.Abstraction.Knowledges.Models;
using BotSharp.Abstraction.MLTasks;
using BotSharp.Abstraction.VectorStorage;

namespace BotSharp.Core.Plugins.Knowledges.Services;

public class KnowledgeService : IKnowledgeService
{
    private readonly IServiceProvider _services;
    private readonly KnowledgeBaseSettings _settings;
    private readonly ITextChopper _textChopper;

    public KnowledgeService(IServiceProvider services,
        KnowledgeBaseSettings settings,
        ITextChopper textChopper)
    {
        _services = services;
        _settings = settings;
        _textChopper = textChopper;
    }

    public async Task Feed(KnowledgeFeedModel knowledge)
    {
        var idStart = 0;
        var lines = _textChopper.Chop(knowledge.Content, new ChunkOption
        {
            Size = 256,
            Conjunction = 32
        });

        var db = GetVectorDb();
        var textEmbedding = GetTextEmbedding();

        await db.CreateCollection(knowledge.AgentId, textEmbedding.Dimension);
        foreach (var line in lines)
        {
            var vec = textEmbedding.GetVector(line);
            await db.Upsert(knowledge.AgentId, idStart, vec, line);
            idStart++;
        }
    }

    public async Task<string> GetAnswer(KnowledgeRetrievalModel retrievalModel)
    {
        var textEmbedding = GetTextEmbedding();
        var vector = textEmbedding.GetVector(retrievalModel.Question);

        // Vector search
        var result = await GetVectorDb().Search(retrievalModel.AgentId, vector);

        // Restore 
        var prompt = "";
        foreach (var knowledge in result)
        {
            prompt += knowledge + "\n";
        }

        prompt += "\r\n###\r\n";
        prompt += "Answer the user's question based on the content provided above, and your reply should be as concise and organized as possible.\r\n";
        prompt += $"\r\nQuestion: {retrievalModel.Question}\r\nAnswer: ";

        var completion = await GetTextCompletion().GetCompletion(prompt);
        return completion;
    }

    public IVectorDb GetVectorDb()
    {
        var db = _services.GetServices<IVectorDb>()
            .FirstOrDefault(x => x.GetType().Name == _settings.VectorDb);
        return db;
    }

    public ITextEmbedding GetTextEmbedding()
    {
        var embedding = _services.GetServices<ITextEmbedding>()
            .FirstOrDefault(x => x.GetType().Name == _settings.TextEmbedding);
        return embedding;
    }

    public ITextCompletion GetTextCompletion()
    {
        var textCompletion = _services.GetServices<ITextCompletion>()
            .FirstOrDefault(x => x.GetType().FullName.EndsWith(_settings.TextCompletion));
        return textCompletion;
    }
}