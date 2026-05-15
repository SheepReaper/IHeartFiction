---
name: dry-refactor-pr
description: Find DRY refactoring opportunities across a repository, score repeated code by repetition count, estimated lines saved, narrowness of scope, and implementation ease, choose the single best opportunity, implement it on a new branch based off main, squash to one commit, and open a pull request. Use when an agent is asked to discover and ship a focused DRY refactor PR while obeying repository guidance, style conventions, branch hygiene, and single-commit PR requirements.
---

# Dry Refactor PR

## Workflow

Use this skill to perform one complete, focused DRY refactor from discovery through pull request.

1. Read repository instructions before changing anything.
   - Inspect root `AGENTS.md`, nested `AGENTS.md` files that apply to touched paths, README/contributor docs, package conventions, and open editor context if provided.
   - Treat those instructions as mandatory. For this repository, obey EF Core, Aspire migration, WolverineFx, and logging guidance when relevant.

2. Establish git and branch state.
   - Run `git status --short --branch` and identify uncommitted user work.
   - Do not overwrite or revert unrelated changes.
   - Fetch the remote main branch if network/auth are available.
   - Create a dedicated branch from main, using the repository's default remote if present:

```bash
git fetch origin main
git switch main
git pull --ff-only origin main
git switch -c dry-refactor/<short-topic>
```

   - If switching would clobber local changes, stop and ask the user how to handle the dirty worktree.

3. Discover repetition broadly, then narrow quickly.
   - Use fast searches and code-aware tools first: `rg`, `rg --files`, language analyzers, tests, linters, clone detectors, or IDE/compiler diagnostics when available.
   - Look for repeated logic, repeated query shapes, duplicated component markup, repeated validation/mapping/logging, similar tests, and copy-pasted constants.
   - Prefer opportunities with a narrow ownership boundary and an obvious local abstraction over broad architectural refactors.

4. Score each candidate before choosing.
   - Evaluate at least three plausible candidates when the repository is large enough.
   - Record a compact table with:
     - `instances`: count of repeated occurrences.
     - `lines_saved`: estimated net lines removed after adding the shared helper/abstraction.
     - `scope`: narrow, moderate, or broad.
     - `ease`: easy, medium, or hard.
     - `risk`: low, medium, or high.
   - Rank primarily by implementation ease, then by impact. Treat impact as a combination of repetition count and lines saved.
   - Select only the best opportunity. Do not bundle unrelated cleanup.

5. Implement the refactor.
   - Preserve behavior and public contracts unless the user explicitly asked for behavior changes.
   - Match existing project style, naming, nullability, formatting, dependency injection patterns, logging conventions, and test style.
   - Add a helper, extension method, shared component, fixture, factory, or local abstraction only when it clearly reduces meaningful duplication.
   - Keep the edit surface as small as practical.

6. Publish the branch before any build.
   - Push the dedicated work branch upstream before running project builds or tests that build the project. This gives SourceLink a remote branch and avoids noisy errors about missing remote metadata.

```bash
git push -u origin HEAD
```

   - If network or auth is unavailable, report that validation may include SourceLink remote warnings and continue with the narrowest useful local validation.

7. Verify.
   - Run the most relevant tests, build, formatter, or analyzer for touched projects after the branch has been pushed.
   - If tests are unavailable or too expensive, run the narrowest meaningful validation and state the limitation.
   - For schema/model changes, apply the repository's migration guardrails exactly. Do not create EF migrations unless the refactor actually requires them.

8. Prepare a single-commit PR.
   - Commit freely while working if useful, but before opening the PR ensure the branch contains exactly one commit on top of main.
   - Use interactive or non-interactive squash as appropriate:

```bash
git fetch origin main
git rebase origin/main
git reset --soft origin/main
git commit -m "Refactor <area> duplication"
git push --force-with-lease
```

   - Push the squashed commit before re-running focused validation or any command that builds the project.
   - Re-run focused validation after the final squashed commit has been pushed.
   - Confirm with `git log --oneline origin/main..HEAD` that there is one commit.

9. Open the pull request.
   - Open a PR from the already-pushed branch with the repository's normal tool, usually `gh pr create`.
   - PR body must summarize:
     - the selected opportunity and why it ranked best;
     - the repeated instances addressed;
     - the refactoring performed;
     - validation run;
     - confirmation that the PR contains a single commit.
   - If GitHub auth or network access is unavailable, leave the branch and single commit ready, then provide the exact command the user can run.

## Candidate Selection Rules

Favor candidates that are easy and low-risk even if another candidate saves more lines. A small obvious refactor that can be confidently tested is better than a broad abstraction that might blur domain boundaries.

Avoid selecting:

- generated files, snapshots, migrations, lockfiles, minified assets, vendored code, or external templates;
- repetition that exists for readability or explicitness in tests;
- abstraction across unrelated domains that merely look textually similar;
- changes that require schema, API, or UX behavior changes to justify the refactor.

## Final Response

Report the completed PR link when available. Include the selected opportunity, one-sentence rationale for ranking, validation results, and single-commit confirmation. If no PR could be opened, explain the blocker and name the prepared branch.
