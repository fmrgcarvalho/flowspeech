using FlowSpeech.Core.Models;
using FlowSpeech.Core.Settings;
using FlowSpeech.Core.Text;
using Xunit;

namespace FlowSpeech.Core.Tests;

public class RuleBasedFormatterTests
{
    private static RuleBasedFormatter Create(FlowSettings? s = null)
    {
        s ??= new FlowSettings();
        return new RuleBasedFormatter(() => s);
    }

    [Theory]
    [InlineData("hum, olá a todos", "Olá a todos")]
    [InlineData("então ãh vamos começar", "Então vamos começar")]
    [InlineData("um so I was thinking", "So I was thinking")]
    public void Removes_fillers(string input, string expected) =>
        Assert.Equal(expected, Create().Format(input, WritingStyle.Raw));

    [Theory]
    [InlineData("olá vírgula tudo bem ponto de interrogação", "Olá, tudo bem?")]
    [InlineData("primeira linha nova linha segunda linha", "Primeira linha\nSegunda linha")]
    [InlineData("reunião às três ponto final obrigado", "Reunião às três. Obrigado")]
    [InlineData("hello comma world full stop", "Hello, world.")]
    [InlineData("Olá, vírgula, tudo bem.", "Olá, tudo bem.")]
    public void Converts_spoken_punctuation(string input, string expected) =>
        Assert.Equal(expected, Create().Format(input, WritingStyle.Raw));

    [Fact]
    public void Applies_personal_dictionary_case_insensitively()
    {
        var result = Create().Format("estou a testar o Flow Speech do miguel gaiao", WritingStyle.Raw);
        Assert.Equal("Estou a testar o FlowSpeech do miguel Gaião", result);
    }

    [Fact]
    public void Dictionary_does_not_replace_inside_other_words()
    {
        var s = new FlowSettings();
        s.Dictionary["api"] = "API";
        Assert.Equal("Rapidamente chamo a API", Create(s).Format("rapidamente chamo a api", WritingStyle.Raw));
    }

    [Fact]
    public void Expands_snippets()
    {
        var result = Create().Format("envia para o meu email por favor", WritingStyle.Raw);
        Assert.Equal("Envia para mgaiao25@gmail.com por favor", result);
    }

    [Theory]
    [InlineData("a reunião é às 10:30", "A reunião é às 10:30")]
    [InlineData("custa 1,5 euros", "Custa 1,5 euros")]
    [InlineData("vê em https://miguelgaiao.ai/blog", "Vê em https://miguelgaiao.ai/blog")]
    public void Preserves_times_decimals_and_urls(string input, string expected) =>
        Assert.Equal(expected, Create().Format(input, WritingStyle.Raw));

    [Fact]
    public void Code_style_keeps_lowercase_and_drops_trailing_period()
    {
        var result = Create().Format("git status.", WritingStyle.Code);
        Assert.Equal("git status", result);
    }

    [Fact]
    public void Capitalizes_after_sentence_end()
    {
        Assert.Equal("Olá. Como estás? Bem!", Create().Format("olá. como estás? bem!", WritingStyle.Raw));
    }

    [Fact]
    public void Empty_input_returns_empty() =>
        Assert.Equal(string.Empty, Create().Format("   ", WritingStyle.Raw));
}
