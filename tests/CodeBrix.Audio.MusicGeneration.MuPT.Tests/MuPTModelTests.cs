using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Audio.Midi;
using CodeBrix.Audio.MusicGeneration.Presets;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using Xunit;

namespace CodeBrix.Audio.MusicGeneration.MuPT.Tests;

public class MuPTModelTests
{
    [Fact]
    public void paths_are_absolute_below_the_consuming_application_and_have_unique_names()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "CodeBrix-model-path-test");

        // Act
        var files = MuPTModel.ResolveFiles(root);

        // Assert
        files.Keys.Should().BeEquivalentTo(MuPTModel.FileNames);
        foreach (var pair in files)
            pair.Value.Should().Be(Path.Combine(root, MuPTModel.RelativeModelDirectory, pair.Key));
        MuPTModel.RelativeModelDirectory.Should().Be("Models/CodeBrix.Audio.MusicGeneration.MuPT");
        var mutable = (IDictionary<string, string>)files;
        Action change = () => mutable.Add("extra", "elsewhere");
        change.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void registering_twice_is_lazy_and_does_not_replace_the_default_replay()
    {
        // Arrange
        var replay = MusicGeneratorRegistry.Resolve(null);

        // Act
        MuPTModel.Register();
        MuPTModel.Register();

        // Assert
        MuPTModel.IsRegistered.Should().BeTrue();
        MuPTModel.IsLoaded.Should().BeFalse();
        MusicGeneratorRegistry.Resolve(MuPTModel.GeneratorName).Should().BeSameAs(MuPTModel.Instance);
        MusicGeneratorRegistry.Resolve(null).Should().BeSameAs(replay);
        MusicGeneratorRegistry.Registered.Count(item => ReferenceEquals(item, MuPTModel.Instance)).Should().Be(1);
        MuPTModel.IsAvailable.Should().BeTrue("the package build targets copy the staged artifacts");
        foreach (var file in new[] { "LICENSE", "THIRD-PARTY-NOTICES.txt", "MODEL-CARD.md", "MODEL-PROVENANCE.json" })
            File.Exists(Path.Combine(MuPTModel.ModelDirectory, file)).Should().BeTrue();
    }

    [Fact]
    public async Task the_copied_model_generates_notes_without_a_staging_path()
    {
        // Arrange
        MuPTModel.Register();
        var request = MuPTPresets.WaltzDuetInAMinor.CreateRequest();
        request.Controls.MaximumEvents = 96;
        request.Seed = 29;
        var notes = 0;

        // Act
        try
        {
            await foreach (var item in MuPTModel.Instance.GenerateAsync(request, TestContext.Current.CancellationToken))
                if (item.Event is NoteOnEvent) notes++;
        }
        finally
        {
            MuPTModel.Instance.Release();
        }

        // Assert
        notes.Should().BeGreaterThan(0);
        MuPTModel.IsLoaded.Should().BeFalse();
    }
}
