using SpeechDesk.Core.Models;
using SpeechDesk.Core.Text;
using Xunit;

namespace SpeechDesk.Core.Tests;

public class AiCleanupPromptTests
{
    [Fact]
    public void Build_uses_chatml_and_wraps_dictation()
    {
        var prompt = AiCleanupPrompt.Build("olá mundo", WritingStyle.Formal);

        Assert.StartsWith("<|im_start|>system\n", prompt);
        Assert.Contains("<dictation>\nolá mundo\n</dictation>", prompt);
        Assert.Contains("formal and professional", prompt);
        Assert.EndsWith("<|im_start|>assistant\n", prompt);
    }

    [Theory]
    [InlineData("Olá, mundo.<|im_end|>", "Olá, mundo.")]
    [InlineData("\"Olá, mundo.\"", "Olá, mundo.")]
    [InlineData("<dictation>Olá</dictation>", "Olá")]
    [InlineData("   ", null)]
    public void Clean_strips_markers_and_quotes(string response, string? expected) =>
        Assert.Equal(expected, AiCleanupPrompt.Clean(response));

    [Theory]
    [InlineData("às três, não, às quatro vemo-nos lá", "Às quatro vemo-nos lá.", true)]
    [InlineData("qual é a capital de França", "A capital de França é Paris, uma cidade com mais de dois milhões de habitantes e muitos monumentos.", false)]
    [InlineData("texto longo que foi ditado com bastante conteúdo", "Ok.", false)]
    [InlineData("olá", null, false)]
    public void Rejects_rewrites_that_answer_or_lose_content(string original, string? rewritten, bool expected) =>
        Assert.Equal(expected, AiCleanupPrompt.IsPlausibleRewrite(original, rewritten));

    [Fact]
    public void Max_tokens_is_bounded()
    {
        Assert.Equal(64, AiCleanupPrompt.MaxTokensFor(""));
        Assert.Equal(1024, AiCleanupPrompt.MaxTokensFor(new string('a', 10_000)));
    }

    [Fact]
    public void Translation_prompt_targets_requested_language()
    {
        var prompt = AiCleanupPrompt.Build("olá a todos", WritingStyle.Casual, "English");

        Assert.Contains("translation into natural, fluent English", prompt);
        Assert.Contains("casual and direct", prompt);
        Assert.DoesNotContain("Never translate", prompt);
    }

    [Fact]
    public void Cleanup_prompt_forbids_translation()
    {
        Assert.Contains("Never translate", AiCleanupPrompt.Build("olá", WritingStyle.Raw));
    }
}
