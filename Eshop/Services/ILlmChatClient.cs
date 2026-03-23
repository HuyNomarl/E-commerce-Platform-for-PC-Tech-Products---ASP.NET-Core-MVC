namespace Eshop.Services
{
    public interface ILlmChatClient
    {
        Task<string> AskAsync(string systemPrompt, string userPrompt);
    }
}
