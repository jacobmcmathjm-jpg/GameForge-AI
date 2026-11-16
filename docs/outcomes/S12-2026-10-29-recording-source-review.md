# Recorded-audio source shortlist — 29 October 2026

The user explicitly permits reuse of reviewed free recordings. This authorizes pursuing that route, not treating every free download as cleared, marking human listening accepted, or bypassing ForgeCore's host/quarantine/import gates. No third-party audio has been downloaded or imported in the v25 candidate.

**30 October follow-up:** the user approves the procedural gunshot and agrees to the actual recorded-footstep source. [The separate v26 increment](S12-2026-10-30-recorded-footsteps.md) downloads/imports six exact GboxMikeFozzy sources through the existing quarantine policy and adds a bundled offline recording candidate. v25 remains unchanged; the historical shortlist decisions below describe the earlier v25 state. No other recording pack is downloaded and no human listening approval of the new footsteps is inferred.

## Sources inspected

| Candidate | Source evidence | Current decision |
| --- | --- | --- |
| Free Firearm Sound Library — Ben Jaszczak, Brian Nelson, Kevin Heras and Matthew Nanney | [OpenGameArt listing](https://opengameart.org/content/the-free-firearm-sound-library) identifies the authors and CC0; the [archived creators' homepage](https://web.archive.org/web/20141217041948/http://freefirearmsfx.com/) independently states CC0 and personal/professional reuse. | Promising source, not a selected/accepted rifle cue. The offered prepared archive is approximately 194 MB, 7z: outside the current runner's 100 MiB limit and passive individual-file inspection types. Do not raise limits or extract arbitrary archives silently. Seek an authorized bounded intake step; record exact archive/selected-file hashes and preserve notices. |
| Footsteps — GboxMikeFozzy | [Creator's OpenGameArt upload](https://opengameart.org/content/footsteps-0) describes recording their own walk through a subway, labels six individual OGG files CC0 and permits reuse. | Small passive-file candidate for a hard-floor audition. Exact files have not been downloaded, decoded, inspected or heard. It must not be labelled a five-surface boot library or certified for the courtyard merely because the page says footsteps. |
| Fantozzi / qubodup footsteps | [OpenGameArt collection](https://opengameart.org/content/fantozzis-footsteps-grasssand-stone) supplies twelve sliced footsteps and CC0; the [original author's description](https://freesound.org/people/Fantozzi/sounds/166306/) says these are handmade dry walking foley using corks, not field recordings. | Potentially useful recorded foley, not authentic recordings of boots on the named surfaces. Do not misrepresent the technique. Its 7z/FLAC packaging also needs a separate bounded intake path. |
| Kenney Impact Sounds | [Official asset page](https://kenney.nl/assets/impact-sounds) and [official support](https://kenney.nl/support) state CC0 for the pack/assets. | General impact/foley shortlist only. Its page alone does not establish realistic rifle shots or suitable footsteps. No blanket quality acceptance. |

These are evidence summaries, not guarantees of ownership or legal advice. A page licence label does not prove every file's provenance. Preserve the checked source URL, creator, licence evidence, acquisition date, original hash, derivative hash and processing recipe for anything ultimately selected. Do not use sounds ripped from commercial games.

## Required reusable integration step

1. Configure/approve only the exact source/download host through ForgeCore's normal asset policy; quarantine bounded passive audio without executing content. The current verification runner has no asset hosts enabled. Do not use an unrestricted shell download to evade it.
2. Inspect actual bytes/format and exact source hash; decode/resample through a bounded reviewed audio pipeline, with duration/channel/PCM-budget limits. OGG signature validation alone is not an audio-quality or decoder-safety certification.
3. Produce normalized, faded, non-clipping derivatives plus listening auditions. Keep source and derivative identities distinct; record trim/gain/resample steps.
4. Add a reusable reviewed-bank contract and event/surface mappings to the generator, retaining procedural fallback and explicit unreviewed/missing-cue diagnostics. Generic source staging/import currently does **not** wire audio automatically into this demo or certify a recording library.
5. Verify missing/tampered/revoked sources fail closed, repeated generations reuse only compatible reviewed versions, and fresh standalone gameplay schedules the intended cues. Obtain human listening/mix acceptance before describing them as realistic or final.

v25's local procedural bank, deterministic variation, travel/contact clock, explicit surface tags and bounded spatial voices are the tested foundation for this next increment. They do not yet implement the reviewed-recording bank adapter above.
