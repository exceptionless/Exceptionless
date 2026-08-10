---
name: releasenotes
description: Evaluate semantic-version impact and generate formatted changelogs from git history since the last release tag. Use when preparing or creating a release, choosing the next version, or drafting release notes that categorize breaking changes, features, fixes, and other changes.
---

Generate a changelog for all changes from the most recent release until now.

## Steps

1. Find the most recent release tag using `git tag --sort=-creatordate`
2. Get commits and merged PRs since that tag
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
11. Draft for user review only; do not create a tag or publish a GitHub release without explicit approval

When version impact is uncertain, ask rather than silently choosing the smaller increment. Do not treat commit labels alone as authoritative; evaluate the actual behavior described by the changes.

## Output

State the recommended semantic-version impact and why. If a version decision is required, present the changelog with the version marked as pending and ask for the version. Otherwise, present the patch-version changelog in a markdown code block for review.
