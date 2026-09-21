================================================================================
staging/ - where the model stager works
CodeBrix.Audio.MusicGeneration.MuPT
================================================================================

This folder is the model stager's workshop. EVERYTHING the stager downloads,
converts, quantizes and writes stays inside this repository: the default model
store under the user's home directory is never opened and never touched, and the
system temporary directory is redirected in here before the first call into the
model libraries, because the conversion and the quantization each lay hundreds of
megabytes out under it and a Debian laptop's /tmp is a small in-memory file
system.

Only this file is checked in. Everything else here is ignored by git.


THE FOLDERS
-----------
  download/   Reserved for a downloader that keeps its downloads apart from its
              store. The model library does not: a partly downloaded file is a
              sidecar beside the blob it will become, inside store/. The folder
              is created, is ignored by git, and normally stays empty.

  store/      The model store - blobs and manifests, laid out exactly as Ollama
              lays its own out. It holds the upstream checkpoint as it was
              pulled, the converted GGUF model and the quantized one. It is
              working material: deleting it costs a re-download, nothing else.

  tmp/        The temporary directory the convert and quantize steps lay their
              work out under. Empty between steps; deleting it is always safe.

  output/     THE ARTIFACT THAT SHIPS, and the only folder here that matters
              once a run is over. A later phase's tests read it and the package
              build takes the model from it. NEVER delete it. The stager itself
              never clears it.


RUNNING THE STAGER
------------------
From the repository root:

    dotnet run --project tools/ModelStager -c Release

It reports what it is doing as it goes, verifies what it produced against the
size and the sha256 recorded for this machine, writes MODEL-PROVENANCE.json at
the repository root, and prints a usage report: what it downloaded, how large
this folder grew, and how long each step took.

    --clean-after     clear download/, store/ and tmp/ when the run succeeds,
                      without asking. output/ is always kept.
    --keep            never ask and never clear; leave everything in place.
    --help            what the tool does and what it takes.

With neither flag, a run at an interactive console offers to clear the three
working folders when it finishes.

A run from an EMPTY staging/ downloads about 380 MB and needs roughly 2.5 GB of
free disk space at its peak. It checks for that space before it starts. A second
run costs no download: the files are already in store/.


WHAT THE STAGER DOES
--------------------
  1. Pulls the upstream checkpoint from Hugging Face, pinned to one commit.
  2. Converts it to GGUF at the checkpoint's own precision - the whole thing is
     managed code, and no Python is involved at any point.
  3. Quantizes that to Q4_K_M through the inference engine's own quantizer.
  4. Copies the one file that ships into output/ and checks it.
  5. Writes MODEL-PROVENANCE.json: where the model came from, the commit, the
     licence, the settings of every step, the size and sha256 of what shipped,
     and the operating system and processor it was staged on.

Re-staging on the SAME machine reproduces the same bytes, and that is the gate.
Bytes are never pinned across platforms: a different operating system or
processor may legitimately produce a different file, and the provenance records
which machine the recorded hash belongs to.
