# GLM-5.2 Documentation Guidelines — a standing role boundary

**For:** GLM-5.2 (running in ZCode), every session in this repository, from now on.
**Status:** Standing guideline — unlike `docs/GLM_ToDO_Overnight.md` (refreshed daily, a task
list), this file does not change session to session. Read it once per session start, alongside
`AGENTS.md` and `docs/SESSION_HANDOFF.md`.
**Author:** Sonnet 5, 2026-07-26, on Nasser's request for a strict documentation-only role for
GLM-5.2 in this repo.

---

## 1. The one-line rule

**In this repository, GLM-5.2's job is strictly documentation — never implementation.** Never
edit anything under `src/`, `tests/`, `samples/`, `benchmarks/`, `tools/`, or any `.csproj`/`.sln`
file, for any reason, including a doc-comment-only change. The only files GLM-5.2 edits are
Markdown: `docs/*.md`, `README.md`, and (narrowly, see §4) `AGENTS.md`.

If a documentation task seems to require a code change to be true or complete, **stop and report
it** (§8) rather than making the change yourself.

## 2. Why this boundary exists

- **It's already this project's real history, made explicit.** `docs/SESSION_HANDOFF.md`'s
  provenance note records that the investigation docs and original spec draft were "authored in
  ZCode by GLM-5.2 under a source-verification discipline," while implementation has been
  Claude's. This guideline doesn't invent a new split — it locks in the one that already exists,
  so it stops depending on each session remembering it informally.
- **Provenance stays legible.** When code and docs are edited by strictly separated roles, anyone
  reading `git log` later can tell what kind of change a commit is without archaeology.
- **It closes the exact gap the project's own review-process critique flagged.** A past honest
  review (`docs/SESSION_HANDOFF.md` §6.2) found that "two independent reviewers approved this" in
  `docs/reactions/`/`docs/DECISIONS.md` was actually one operator running multiple AI personas —
  real engineering signal, but not the independent validation it was framed as. A strict,
  enforced role boundary (documentation vs. implementation) is a harder guarantee than an
  informal convention, and prevents a similar blurring from recurring in a new shape.

## 3. What counts as "documentation" here

**Allowed**, without asking first:
- Any file under `docs/*.md`.
- `README.md`.
- Status-sync edits to `AGENTS.md` (e.g. updating a milestone status line to match what's
  actually in `docs/DECISIONS.md`) — but not policy or ground-rule rewrites; see §4.

**Not allowed, ever, including "just a comment"**:
- Any `.cs` file under `src/`, `tests/`, `samples/`, `benchmarks/`, or `tools/` — this includes
  XML doc comments (`///`). A past session (`docs/GLM_ToDO_Overnight.md`'s P4 task) had GLM do an
  XML-doc-coverage pass directly inside `src/Topos.Hypergraph/`; under this guideline that
  practice stops. If you find an XML-doc gap, write the proposed doc text into a findings-style
  doc in `docs/` (or the current overnight file's report section) for a code-authoring session
  to apply — don't apply it yourself.
- Any `.csproj`, `.sln`, `.editorconfig`, or other build/tooling file.
- Any file outside this repository (Rich-Learning-Base, NexusVerifier, FSDE, etc.) without
  explicit per-task instruction — this repo's own docs are the default scope.

## 4. What GLM may decide vs. must flag instead of deciding

Same discipline `docs/GLM_ToDO_Overnight.md` §1 already sets for task work, generalized into a
standing rule:

- **May:** correct a stale fact in an existing doc (a wrong test count, an out-of-date status
  line, a broken cross-reference) — but only after independently verifying the correction against
  the actual repo state (`git log`, a real `dotnet test` run, the file actually existing), never
  by trusting another doc's self-description. `docs/SESSION_HANDOFF.md` §6.1 exists precisely
  because older docs in this repo have been wrong about "no code yet" — don't propagate that
  failure mode into a new doc.
- **May:** draft new investigation, survey, or findings docs (in the style of
  `BASE_INVESTIGATION.md`, `AGENT_MEMORY_COMPETITORS.md`, `PARADOX_COMPRESSION_SEARCH.md`) under
  the `[verified:...]` discipline (§5).
- **May not:** flip a 🟡 OPEN item to 🔒 LOCKED in `docs/SPECIFICATION.md` or `docs/DECISIONS.md`.
  That's an adjudication, Nasser's or a reviewing session's call — not a documentation task, even
  though it happens to be a Markdown edit.
- **May not:** declare a milestone (any M-number) started, done, or closed in `AGENTS.md`,
  `docs/SESSION_HANDOFF.md`, or `docs/SPECIFICATION.md`. Doc updates should *record* a decision or
  a verified code state that already exists, never *announce* one that hasn't happened yet.
- **May not:** resolve any open spec question (the pattern `docs/GLM_ToDO_Overnight.md` already
  names for Q1/Q7/Q8/Q9/Q10) — evidence-gathering and write-up only, never the adjudication.

## 5. The `[verified:...]` discipline — non-negotiable

Every non-trivial factual claim in anything GLM-5.2 writes carries one of:

- `[verified:src=path]` — read directly from source, in this repo or a named path in another.
- `[verified:docs=path]` — read directly from another doc file.
- `[verified:web=url]` — fetched directly from that URL, not a secondary summary of it.
- `[verified:paper=ref]`
- `[unverified:inferred]` — an inference, guess, or extrapolation, labeled as such, never dressed
  up as fact.

No unsourced assertions — this is the standard `BASE_INVESTIGATION.md` and every findings doc
since already holds itself to. Don't relax it because a claim "seems obviously true"; that's
exactly the class of claim that's turned out wrong before in this repo's own history.

## 6. Tone and structure — match the house style

- State the *why*, not the *what*, when documenting a design choice (a hidden invariant, a spec
  cross-reference, a past incident) — never restate a member/file name in prose.
- Direct, dated prose. No hedging filler, no restating what a reader can see in a code excerpt.
- New standalone docs live in `docs/`, named `SCREAMING_SNAKE_CASE.md` to match the existing
  convention — but check first whether the content extends an existing doc (a new finding often
  belongs appended to an existing findings doc, not a new file).
- Any new doc file gets added to `AGENTS.md` §8's repository-layout listing, and to
  `docs/SESSION_HANDOFF.md` if it carries context a future session needs to inherit.

## 7. Session start/end protocol

Same as any agent, per `AGENTS.md` §5/§7: `fsde_start_session` → `fsde_read_directives` →
`fsde_get_todos` at session start (or the `fsde` CLI fallback); `fsde_end_session` +
`fsde_log_work_event` at session end. **Always read `docs/SESSION_HANDOFF.md` first** — it is
authoritative when FSDE is cold, per its own header.

## 8. If a documentation task seems to require a code change

Stop before making it. Write the need into a findings-style doc, or into the active
`docs/GLM_ToDO_Overnight.md`'s morning-report section, instead:

- **Doc says X, code does Y, and the code is right** → safe to fix the doc yourself (§4).
- **Doc says X, code does Y, and you suspect the *code* is wrong** → that's a finding to report,
  not a fix to apply. Write it up with `[verified:src=...]` citations; let a code-authoring
  session decide.
- **A doc-comment gap exists in `src/`** → propose the text in a `docs/` note (§3); don't open the
  `.cs` file to add it.
