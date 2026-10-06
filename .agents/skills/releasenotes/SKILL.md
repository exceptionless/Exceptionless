---
name: releasenotes
description: Evaluate semantic-version impact and generate formatted changelogs from git history since the last release tag. Use when preparing or creating a release, choosing the next version, or drafting release notes that categorize breaking changes, features, fixes, and other changes.
---

Generate a changelog for all changes from the most recent release until now.

## Mandatory release gates

These gates apply even when the user initially asks to create or publish a release and supplies the version.

1. **Start from freshly fetched main.** Before choosing the release range, generating notes, or selecting a target SHA, run `git fetch origin --prune --tags`. Stop if the fetch fails. Resolve the release endpoint from `refs/remotes/origin/main`; never use the current checkout, local `main`, `HEAD`, a previously cached `origin/main`, or an older commit merely because it is already available locally.
2. **Show the exact notes and stop.** Present the complete release notes to the user in a markdown code block, then end the turn without creating a tag, draft release, or published release. The initial release request, a supplied version, or approval of the production-deployment side effect does not approve unseen release notes. Only a subsequent user message approving the displayed notes authorizes publication.
3. **Re-fetch immediately before publication.** After the notes are approved and immediately before creating the tag or GitHub release, run `git fetch origin --prune --tags` again and resolve `refs/remotes/origin/main` again. Stop if the fetch fails.
4. **Invalidate approval when main moves.** Compare the freshly fetched `origin/main` SHA with the SHA used for the approved release range. If they differ, do not publish. Inspect the new commits, regenerate the notes, show the revised notes, and wait for a new subsequent approval.
5. **Target and verify latest main.** Create the release with the exact SHA from the final successful fetch, for example `gh release create <tag> --target <sha> --notes-file <file>`. After creation, verify that the release tag resolves to that SHA. Do not claim success if the target verification fails.

Any change to the version, notes, or target SHA invalidates the prior approval.

## Steps

1. Complete the initial latest-main fetch gate above.
2. Find the most recent release tag using `git tag --sort=-creatordate` and get commits and merged PRs through the freshly fetched `refs/remotes/origin/main`.
3. Look at previous releases in this repo to match their format and style
4. Evaluate every change and classify the highest semantic-version impact in the range:
   - **Patch:** Bug fixes, maintenance, internal refactors, documentation, minor UI polish, and small improvements that do not add a meaningful new user-facing capability.
   - **Minor:** Any substantive user-facing feature or meaningful new capability. Small improvements may remain patch-level when they do not materially expand what users can do.
   - **Major:** Breaking changes, deliberately incompatible behavior, or unusually large features that materially redefine the product or its public contracts.
5. Use the highest-impact change to recommend the next version. Mixed ranges take the highest classification.
6. If the range contains any substantive feature, breaking change, unusually large feature, or ambiguous version impact, explain the classification and recommend a minor increment for normal features or a major increment for breaking changes and unusually large features. If the user already supplied a version, honor it and state any mismatch with the recommendation without asking them to repeat or reconfirm it. Otherwise, ask what the next version should be before assigning a version, creating a tag, or publishing a release.
7. If the range is patch-only, use the next patch version unless the user supplied a different version or the repository follows another established versioning scheme.
8. Categorize changes into sections: Breaking Changes, Added, Changed, Fixed, Notes
9. Focus on user-facing changes, important bug fixes, and breaking changes
10. Include PR links and contributor attribution
11. Complete the notes-review stop above. Publication is a later-turn action that must also pass the final latest-main gates.

When version impact is uncertain, ask rather than silently choosing the smaller increment. Do not treat commit labels alone as authoritative; evaluate the actual behavior described by the changes.

## Output

State the recommended semantic-version impact and why. If a version decision is required, present the changelog with the version marked as pending and ask for the version. Otherwise, present the patch-version changelog in a markdown code block for review. Always end the drafting turn after presenting the notes; never publish in the same turn.
