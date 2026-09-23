CodeBrix.Audio.MusicGeneration.MuPT — NONSHIPPING CONTENT

TOOLS
-----
tools/ModelStager is the reproducible download/conversion/reduction tool. It is
not packable and is never an application runtime dependency. Its package
references are pinned to the versions recorded by the accepted staging recipe.
See staging/README.txt for execution, free space and cleanup.

TESTS AND VALIDATION
--------------------
tests/CodeBrix.Audio.MusicGeneration.MuPT.Tests uses xUnit v3 and SilverAssertions. Short packaged-model
generation, path/registration behavior and artifact integrity belong there.
Long rendering/listening gates are in MusicGeneration. Scratch applications,
local validation packages and logs belong under ignored TestResults/.

DOCUMENTATION
-------------
MODEL-CARD.md is an unchanged pinned publisher document; do not rewrite it as
our documentation. Update THIRD-PARTY-NOTICES.txt when changing model provenance.
The README family and all eight agent pointers follow the CodeBrix.SSH pattern.
