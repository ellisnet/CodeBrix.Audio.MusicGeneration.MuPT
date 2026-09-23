# CodeBrix.Audio.MusicGeneration.MuPT

The MuPT Q4_K_M GGUF model for [CodeBrix.Audio.MusicGeneration](https://github.com/ellisnet/CodeBrix.Audio.MusicGeneration), packaged with a small registration assembly. It generates ABC music that MusicGeneration converts to streaming MIDI.

```csharp
using CodeBrix.Audio.ModestSynth;
using CodeBrix.Audio.MusicGeneration;
using CodeBrix.Audio.MusicGeneration.MuPT;

GeneralMidiInstrumentLibrary.Register();
MuPTModel.Register();
using var music = new MusicSession(new MusicGenerationOptions { Generator = MuPTModel.GeneratorName });
music.Play();
```

Registration loads nothing. The model loads on first use or explicit preloading. The package copies its model and notices into `Models/CodeBrix.Audio.MusicGeneration.MuPT/` in build and publish output, including when referenced through an intermediary library. MuPT and SkyTNT can be installed together. Registering does not select a generator; specify its name.

Inference assets: **117.44 MiB** before NuGet compression. File size is not a runtime-memory limit. No runtime downloads, ModelManager or Python are required. MuPT uses the native inference engine bundled with ModelRunner.

See [README-INDEX.txt](README-INDEX.txt) for consumer and maintainer documentation, and [MODEL-PROVENANCE.json](MODEL-PROVENANCE.json) for the reproducible recipe. The wrapper and model are Apache-2.0 licensed; attribution is in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Release preparation is in progress. The published MusicGeneration dependency and final consuming-package gates must be verified before publication.
