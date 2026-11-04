using System.Runtime.CompilerServices;
using Anthropic;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using StandardMessages = Anthropic.Models.Messages;

namespace GpOne.Services;

/// <summary>
/// Chat in streaming con Anthropic Claude tramite l'SDK ufficiale.
/// Sui modelli che lo supportano attiva il fallback lato server: se Claude rifiuta la richiesta
/// per policy, la risposta viene servita da un modello di riserva nella stessa chiamata.
/// </summary>
internal static class ClaudeChat
{
    private const string MessaggioRifiuto = "\n\n[Il modello ha rifiutato di rispondere a questa richiesta.]";

    // Modelli che accettano il fallback lato server (forma "default")
    private static readonly HashSet<string> ModelliConFallback =
        new(StringComparer.OrdinalIgnoreCase) { "claude-fable-5-1", "claude-opus-5-5", "claude-opus-5", "claude-sonnet-5-5" };

    public static IAsyncEnumerable<string> StreamAsync(
        string apiKey, AiImpostazioni impostazioni, IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken) =>
        ModelliConFallback.Contains(impostazioni.Modello)
            ? StreamConFallbackAsync(apiKey, impostazioni, history, cancellationToken)
            : StreamStandardAsync(apiKey, impostazioni, history, cancellationToken);

    private static async IAsyncEnumerable<string> StreamConFallbackAsync(
        string apiKey, AiImpostazioni impostazioni, IReadOnlyList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = new AnthropicClient { ApiKey = apiKey };

        var parameters = new MessageCreateParams
        {
            Model = impostazioni.Modello,
            MaxTokens = 64000,
            System = impostazioni.SystemPrompt,
            Messages = [.. history.Select(m => m.Role == ChatRole.User
                ? (BetaMessageParam)new BetaUserMessageParam { Content = m.Text }
                : new BetaAssistantMessageParam { Content = m.Text })],
            // Fallback lato server (beta), forma "default": il modello di riserva è scelto in base al motivo del rifiuto
            Fallbacks = new Default(),
            Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
        };

        await foreach (BetaRawMessageStreamEvent streamEvent in client.Beta.Messages.CreateStreaming(parameters).WithCancellation(cancellationToken))
        {
            if (streamEvent.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var text))
                yield return text.Text;
            else if (streamEvent.TryPickDelta(out var messageDelta) && messageDelta.Delta.StopReason == "refusal")
                yield return MessaggioRifiuto;
        }
    }

    private static async IAsyncEnumerable<string> StreamStandardAsync(
        string apiKey, AiImpostazioni impostazioni, IReadOnlyList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = new AnthropicClient { ApiKey = apiKey };

        var parameters = new StandardMessages.MessageCreateParams
        {
            Model = impostazioni.Modello,
            MaxTokens = 64000,
            System = impostazioni.SystemPrompt,
            Messages = [.. history.Select(m => m.Role == ChatRole.User
                ? (StandardMessages.MessageParam)new StandardMessages.UserMessageParam { Content = m.Text }
                : new StandardMessages.AssistantMessageParam { Content = m.Text })],
        };

        await foreach (StandardMessages.RawMessageStreamEvent streamEvent in client.Messages.CreateStreaming(parameters).WithCancellation(cancellationToken))
        {
            if (streamEvent.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var text))
                yield return text.Text;
            else if (streamEvent.TryPickDelta(out var messageDelta) && messageDelta.Delta.StopReason == "refusal")
                yield return MessaggioRifiuto;
        }
    }
}
