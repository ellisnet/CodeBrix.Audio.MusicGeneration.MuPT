CodeBrix.Audio.MusicGeneration.MuPT — CONSUMER GUIDE
===================================

WHAT THE PACKAGE DOES
---------------------
CodeBrix.Audio.MusicGeneration.MuPT.ApacheLicenseForever contains the MuPT Q4_K_M GGUF inference artifacts and
an explicit registration wrapper. Target framework: .NET 10. Its only direct
package dependency is CodeBrix.Audio.MusicGeneration.MitLicenseForever.
MusicGeneration brings Audio, ModestSynth and ModelRunner. ModelManager and
Python are not application runtime requirements. No model is downloaded at runtime.

GET STARTED
-----------
    using CodeBrix.Audio.ModestSynth;
    using CodeBrix.Audio.MusicGeneration;
    using CodeBrix.Audio.MusicGeneration.MuPT;

    GeneralMidiInstrumentLibrary.Register();
    MuPTModel.Register();
    using var music = new MusicSession(new MusicGenerationOptions
    {
        Generator = MuPTModel.GeneratorName,
        InstrumentLibrary = "ModestSynthGm"
    });
    music.Play();

Both registrations are explicit. Registering the model is idempotent and loads
nothing. Without Generator set, MusicGeneration plays its embedded replay.
An application can instead choose FluidR3Gm or another registered instrument
library, and use Audio's existing program substitutions and rendition assignments.
The model package neither selects nor modifies the application's instruments.

PUBLIC API — MuPTModel
-------------------------
GeneratorName, Family, PackageId, LicenseId, RelativeModelDirectory: constants.
FileNames: immutable inference-artifact names.
ModelDirectory: absolute default output directory.
ModelPath: absolute GGUF path.
Instance: shared MuPTMusicGenerator; constructing it loads nothing.
IsRegistered: whether that instance is in MusicGeneratorRegistry.
IsLoaded: whether inference weights are loaded.
IsAvailable: existence of all artifacts, not a hash or load validation.
Register(): idempotent; a conflicting generator name is an error.
ResolveDirectory(applicationDirectory = null): resolve below an output root.
ResolveFiles(applicationDirectory = null): immutable logical-name-to-path map.

    await MuPTModel.Instance.PreloadAsync(cancellationToken);
    // Finish/dispose sessions and active generations before releasing weights.
    MuPTModel.Instance.Release();

Use Release to unload the shared registered instance. Do not Dispose that shared
instance and then expect registration to resurrect it. For isolated ownership or
custom inference options, construct a MuPTMusicGenerator with a unique name
using the ModelPath and register it yourself.

OUTPUT AND PUBLISH LAYOUT
-------------------------
    Models/CodeBrix.Audio.MusicGeneration.MuPT/
        MuPT-v1-8192-190M-Q4_K_M.gguf
        LICENSE
        THIRD-PARTY-NOTICES.txt
        MODEL-CARD.md
        MODEL-PROVENANCE.json

buildTransitive targets copy these files to the build AND publish output of any
.NET consumer, including an executable referencing a library that references
this package. The other model package uses a different directory and registry
name. Files remain external: they are not embedded in an assembly or single-file
executable. Deploy the model directory with the application.

To manage assets yourself, set CodeBrixMuPTCopyAssetsToOutput=false before
NuGet targets are imported, and construct the core adapter with your asset path.
CodeBrixMuPTAssetsDirectory changes the source asset folder used by the build.
Neither setting downloads or stages a model.

MODEL, PROMPTS AND RUNTIME
-------------------------
MuPT emits ABC notation. Use MuPTPresets or MusicRequest.ModelNativeText; arbitrary natural-language prose is refused. Its GGUF is read by ModelRunner's bundled native engine. The native library matching the deployment platform must be retained.
Construction and registration are inexpensive; loading and inference are not.
The package's 117.44 MiB of model assets does not bound working set,
attention state, temporary buffers or total application memory. MusicGeneration
buffers and may fall back to complete segments if generation cannot keep pace.
See its consumer guide for capabilities, follow-ups, cancellation and rendering.

PROVENANCE AND LICENSE
----------------------
Source: https://huggingface.co/m-a-p/MuPT-v1-8192-190M
Revision: bf8f270d11683f65aa83ccebc2e756b782b795d8
The unmodified publisher model card, Apache-2.0 license and modifications notice
travel with the model. MODEL-PROVENANCE.json lists input/output hashes, tools and
staging platform. Re-staging is a maintainer operation; see MAINTAINER-README.txt.
