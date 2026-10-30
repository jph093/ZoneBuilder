# Publishing to Paradox Mods

Step-by-step, exact commands. Written 2026-10-30 after a session wasted
real upload attempts (and one accidental Paradox Mods metadata write)
rediscovering all of this. Follow this literally next time instead of
re-deriving it.

## 0. Prerequisites

- **The game must be fully closed.** The build's deploy step copies the
  compiled DLL into `...\Mods\ZoneBuilder`, which the running game has
  locked. If the game is open, the build fails with `MSB3231: Unable to
  remove directory ... Access ... is denied`. Close it, then proceed.
- You must have logged into Cities: Skylines II with your Paradox
  account at least once on this machine. The publish tool auto-logs-in
  using that cached session (`.pdxsdk` under
  `%LOCALAPPDATA%Low\Colossal Order\Cities Skylines II`) - no email/
  password needs to be typed or passed anywhere for a normal publish.

## 1. Bump the version and changelog

Edit `Properties/PublishConfiguration.xml`:
- `<ModVersion Value="X.Y" />` - **must be strictly different from
  whatever version is currently registered on Paradox's backend**, not
  just different from your last successful content upload. See the
  gotcha in step 3 below - this bites you if a prior `Update` run ever
  touched the version field without a real publish succeeding.
- `<ChangeLog>` - plain-language, player-facing, no implementation
  detail or AI-session narration. Matches the tone of existing entries
  in that file.
- If this release adds/changes a zone base type or similarly visible
  concept, also check the `<LongDescription>` text a few lines up (the
  Zones tab's own description lists out the base types by name).

Commit this on its own, titled `Publish vX.Y: <short summary>` -
matches the existing git history's convention (one commit per feature,
then a separate version-bump commit).

## 2. Run the actual publish command

```bash
dotnet publish ZoneBuilder.csproj -p:ModPublisherCommand=NewVersion
```

That's the whole command. It rebuilds the mod, redeploys it to
`...\Mods\ZoneBuilder`, then invokes
`Cities2_Data\Content\Game\.ModdingToolchain\ModPublisher\ModPublisher.exe`
with the right arguments automatically (wired through
`%CSII_TOOLPATH%\Mod.targets`'s `Publish` MSBuild target - no need to
touch `ModPublisher.exe` directly).

**Use `NewVersion`, never `Update`, to ship an actual code change.**
`-p:ModPublisherCommand=Update` only rewrites the Paradox Mods listing
page's metadata (title, description, changelog text, the version
*number* shown) - confirmed by decompiling `ModPublisher.dll`
(`ilspycmd` against the same file this toolchain ships):
`RunUpdateMetadata` never calls `PrepareContent` at all, unlike
`RunNewVersion`/`RunPublish`. Running `Update` silently leaves the
actual mod code subscribers download unchanged, with no error or
warning that anything was skipped - this is exactly what happened in
the session that prompted this doc, and it's why the gotcha below
exists.

There is also a `Publish` command (`-p:ModPublisherCommand=Publish`) -
that one is for a mod's *first ever* upload (no existing `ModId`).
Never use it once `<ModId>` in `PublishConfiguration.xml` is already
set to a real id (163328 for this mod) - it would attempt to publish a
second, separate mod listing.

## 3. The version-number gotcha

Paradox's backend tracks "the current version" the moment ANY publish
call - including a metadata-only `Update` - sets
`<ModVersion>`. If you (or a prior session) ran `Update` with a given
version number, that number is now "used" even though no content ever
shipped for it. A subsequent `NewVersion` call reusing that same number
fails with:

```
Could not publish new mod version: Staging data is invalid:
- Invalid UserModVersion, value must be different than the existing version's.
```

Fix: bump `<ModVersion>` again to a genuinely new number and re-run
step 2. There's no way to "retract" the stale version number from
Paradox's side via this tool - just move past it.

## 4. Verify

The command's own output ends with `New mod version published` /
`Publishing new version updating process finished successfully` on
success. No further step needed - no separate "confirm" action, no
in-game step, nothing else to click. If you see `Mod metadata Updated`
/ `Mod metadata updating process finished successfully` instead, that
was an `Update` run, not a real content publish - see step 3's gotcha.

## 5. Push to GitHub too

This mod's `ExternalLink` points at
[github.com/jph093/ZoneBuilder](https://github.com/jph093/ZoneBuilder),
and the repo is public per the AI disclosure policy in
`PublishConfiguration.xml`'s `LongDescription`. `git push origin main`
after committing, same as any other change - unrelated to the Paradox
upload itself but should happen for every release regardless.
