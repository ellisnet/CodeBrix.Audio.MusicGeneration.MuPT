CodeBrix.Audio.MusicGeneration.MuPT — MAINTAINER GUIDE
===================================

STAGE BEFORE BUILDING OR PACKING
-------------------------------
The large model files are ignored by Git. GeneratePackageOnBuild=true is
intentional: an unstaged checkout fails at build/pack with the missing path and
the stager command. Never replace a missing model with an empty placeholder.

    dotnet run --project tools/ModelStager -c Release -- --keep

The stager is already implemented. It pins upstream revisions and keeps every
download, conversion and temporary file inside staging/. It requires .NET and
its declared staging packages; no Python is used. The Ollama staging references
now use published version 1.0.266.110. Existing MODEL-PROVENANCE.json keeps the
versions that actually produced the accepted artifacts. Read staging/README.txt for
space requirements and cleanup flags. Do not delete staging/output.

BUILD AND TEST
--------------
Read README-INDEX.txt, then AGENT-README.txt in full before editing. Follow the
standard coding-agent pointers. Do not enable Nullable or ImplicitUsings. Tests
use xunit.v3 4.0.1 and SilverAssertions.ApacheLicenseForever.

The shipping project references published MusicGeneration 1.0.266.178. Restore
from nuget.org; no local core package or version override is required. Keep this
as a NuGet dependency, never a sibling ProjectReference. Jeremy authorized local
model packages for prepublication consumer validation; their dependency chain now
uses published MusicGeneration, Audio, ModestSynth and ModelRunner packages.

    dotnet restore CodeBrix.Audio.MusicGeneration.MuPT.slnx
    dotnet test --solution CodeBrix.Audio.MusicGeneration.MuPT.slnx -c Debug
    dotnet test --solution CodeBrix.Audio.MusicGeneration.MuPT.slnx -c Release
    dotnet build src/CodeBrix.Audio.MusicGeneration.MuPT/CodeBrix.Audio.MusicGeneration.MuPT.csproj -c Release

For an artifact-only check that does not resolve dependencies:
    dotnet msbuild src/CodeBrix.Audio.MusicGeneration.MuPT/CodeBrix.Audio.MusicGeneration.MuPT.csproj -t:ValidateModelArtifacts

PACKAGING CONTRACT
------------------
The csproj follows ModelRunner/ModelManager's CodeBrix metadata order and UTC
version block: 1.<years since 2026>.<day of year>.<minute of day>. Version,
AssemblyVersion and FileVersion share BuildVersion. Do not replace this block
with a hardcoded version. Build once per release minute; avoid publishing two
builds with the same identity. CLI BuildVersion overrides are for reproducible
validation, not a different versioning scheme.

Exactly one direct shipping dependency: MusicGeneration. The staging tools are
nonpackable and are the only consumers of ModelManager in this repository.
The package contains the wrapper DLL/XML, model assets under assets/, root
README/AGENT-README/icon and buildTransitive/net10.0/CodeBrix.Audio.MusicGeneration.MuPT.ApacheLicenseForever.targets.
The target explicitly copies to build and publish, under Models/CodeBrix.Audio.MusicGeneration.MuPT/.
Models, notices and provenance must remain together when deployed.

ValidateModelArtifacts rejects missing files or changed model hashes. Inspect
the zipped layout, content hashes, licence metadata and dependency groups.
Expected uncompressed inference bytes: 123,149,152. Verify the final nupkg is
below nuget.org's package-size limit; never infer that from model size alone.

REPRODUCIBILITY AND RELEASE GATES
-------------------------------
MODEL-PROVENANCE.json is the committed Windows staging record. Preserve it when
validating on another system. Previously accepted Linux and Windows artifacts
match byte for byte on the recorded CPU. This does not establish identical bytes
on every processor. The existing phase-S clean staging runs are the reproducibility
evidence; recheck artifact hashes before packaging. If re-staging changes a hash,
record the new environment and investigate before updating expected values.

Test a two-project consumer: intermediary library references MusicGeneration and
this model package; executable references ONLY that library. Repeat for MuPT only,
SkyTNT only and both. Inspect build and publish outputs, register lazily, select
the generator explicitly and generate using ONLY the copied packaged files.
Short live tests must then resolve from the published model package; long renders
remain opt-in. Complete the planned listening check and retain the output/evidence.
Published-dependency and package-consumption checks remain required before release.

No Git commits, pushes or NuGet publication are performed by this session.
Jeremy reviews and handles publication. See the session PLAN for current status.

LOCAL VALIDATION RECORD — 2026-09-22
----------------------------------
Debug/Release wrapper checks, missing-artifact/hash guards, NuGet layout and
artifact hashes are checked in this session. Temporary NuGets were consumed
directly and through an intermediary NuGet in applications referencing MuPT only,
SkyTNT only and both. All build/publish layouts retained the correct model files
and notices; registration loaded neither model and retained the default replay.
The combined published output generated real notes and 20-second audio renders
without staging-path variables, ModelManager or Python. TestResults holds logs;
the session PLAN records final gate status and package identities.

The short copied-artifact generation test is part of the ordinary suite. It needs
no path variable because the build already requires and copies the staged model.
Long renders and audible playback stay separate opt-in checks.

PUBLISHED CORE VALIDATION — 2026-09-22
------------------------------------
The dependency is now MusicGeneration 1.0.266.178 from nuget.org. Ordinary
Debug/Release suites pass without a version override or local core feed. The
prepared model package uses standard UTC versioning (1.0.266.218); inspection,
all direct/intermediary consumer layouts and generation/audio from packaged
artifacts pass. Accepted model hashes and Windows provenance are unchanged.
See TestResults/published-core-1.0.266.178-* and the session PLAN for evidence.
Jeremy subsequently published MuPT 1.0.266.257. MusicGeneration's core test suite
now references that published package and uses its copied assets without a model
path variable. All 20 short MuPT checks run normally; core Debug/Release and the
affected direct/intermediary consumer checks pass. Long/audible tests remain
opt-in, and the previous listening acceptance remains in effect. See the session
PLAN and core TestResults/published-mupt-1.0.266.257-* for the adoption evidence.
