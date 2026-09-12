using System;
using System.Threading.Tasks;
using Flow.Core.Personalization.Styles;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.Personalization;

public sealed class StyleFormattingTests : IDisposable
{
    private readonly SqlitePersonalizationDatabase _db;
    private readonly SqliteStyleRepository _repo;

    public StyleFormattingTests()
    {
        _db = new SqlitePersonalizationDatabase(":memory:");
        _repo = new SqliteStyleRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task SeededProfiles_AreAvailableOnStartup()
    {
        var profiles = await _repo.GetAllProfilesAsync();
        Assert.NotEmpty(profiles);
        Assert.Contains(profiles, p => p.Name == "Default");
        Assert.Contains(profiles, p => p.Name == "Personal");
        Assert.Contains(profiles, p => p.Name == "Work");
        Assert.Contains(profiles, p => p.Name == "Email");
        Assert.Contains(profiles, p => p.Name == "Technical");
        Assert.Contains(profiles, p => p.Name == "Casual");
    }

    [Fact]
    public void StyleFormattingEngine_ExpandsContractions_WhenPolicyIsExpand()
    {
        var engine = new StyleFormattingEngine();
        var profile = new StyleProfile
        {
            Name = "Formal Email",
            ContractionPolicy = ContractionPolicy.Expand,
            FormalityLevel = FormalityLevel.Formal
        };

        string input = "I don't think we can't do this, and I'm sure it's ready.";
        string output = engine.Format(input, profile);

        Assert.Equal("I do not think we cannot do this, and I am sure it is ready.", output);
    }

    [Fact]
    public void StyleFormattingEngine_ContractsWords_WhenPolicyIsContract()
    {
        var engine = new StyleFormattingEngine();
        var profile = new StyleProfile
        {
            Name = "Casual Chat",
            ContractionPolicy = ContractionPolicy.Contract,
            FormalityLevel = FormalityLevel.Casual
        };

        string input = "I do not think we cannot do this, and I am sure it is ready.";
        string output = engine.Format(input, profile);

        Assert.Equal("I don't think we can't do this, and I'm sure it's ready.", output);
    }

    [Fact]
    public void StyleFormattingEngine_SubstitutesCasualWords_InFormalMode()
    {
        var engine = new StyleFormattingEngine();
        var profile = new StyleProfile
        {
            Name = "Executive Formal",
            ContractionPolicy = ContractionPolicy.Preserve,
            FormalityLevel = FormalityLevel.Formal
        };

        string input = "Yeah, I'm gonna check it and wanna finish today, nope?";
        string output = engine.Format(input, profile);

        Assert.Equal("Yes, I'm going to check it and want to finish today, no?", output);
    }

    [Fact]
    public async Task StyleFormattingEngine_ResolvesApplicationMapping()
    {
        await _repo.SetStyleForAppAsync("devenv", "style_technical");
        await _repo.SetStyleForAppAsync("slack", "style_personal");

        var engine = new StyleFormattingEngine(_repo);
        await engine.ReloadAsync();

        var codeProfile = engine.ResolveProfile("devenv.exe");
        Assert.Equal("Technical", codeProfile.Name);

        var slackProfile = engine.ResolveProfile("slack.exe");
        Assert.Equal("Personal", slackProfile.Name);

        var unknownProfile = engine.ResolveProfile("notepad.exe");
        Assert.Equal("Default", unknownProfile.Name);
    }

    [Fact]
    public async Task SaveProfile_UpdatesExistingProfile()
    {
        var custom = new StyleProfile
        {
            Id = "style_custom_1",
            Name = "My Custom Style",
            ContractionPolicy = ContractionPolicy.Expand,
            FormalityLevel = FormalityLevel.Formal
        };

        await _repo.SaveProfileAsync(custom);

        var retrieved = await _repo.GetProfileByIdAsync("style_custom_1");
        Assert.NotNull(retrieved);
        Assert.Equal("My Custom Style", retrieved.Name);
        Assert.Equal(ContractionPolicy.Expand, retrieved.ContractionPolicy);

        retrieved.ContractionPolicy = ContractionPolicy.Contract;
        await _repo.SaveProfileAsync(retrieved);

        var updated = await _repo.GetProfileByIdAsync("style_custom_1");
        Assert.NotNull(updated);
        Assert.Equal(ContractionPolicy.Contract, updated.ContractionPolicy);
    }
}
