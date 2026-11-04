using System.Runtime.CompilerServices;

namespace GpOne.Services;

/// <summary>
/// Implementazione di prova che simula una risposta in streaming.
/// Usata dal motore "Simulato" di GpOne AI.
/// </summary>
public class SimulatoAiService : IAiChatService
{
    public async IAsyncEnumerable<string> GetResponseStreamAsync(
        IReadOnlyList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lastQuestion = history.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
        var answer = $"Hai scritto: \"{lastQuestion}\". Questa è una risposta simulata: configura un motore in Sistema > Intelligenza AI.";

        foreach (var word in answer.Split(' '))
        {
            await Task.Delay(40, cancellationToken);
            yield return word + " ";
        }
    }
}
