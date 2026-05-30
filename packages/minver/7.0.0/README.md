# MinVer

![MinVer](https://raw.githubusercontent.com/adamralph/minver/28ce6ec69c4489c034f7eea78665a0cc9980eb81/assets/minver.svg)

_[![MinVer NuGet version](https://img.shields.io/nuget/v/MinVer.svg?style=flat&label=nuget%3A%20MinVer)](https://www.nuget.org/packages/MinVer)_
_[![minver-cli NuGet version](https://img.shields.io/nuget/v/minver-cli.svg?style=flat&label=nuget%3A%20minver-cli)](https://www.nuget.org/packages/minver-cli)_

_[![CI](https://github.com/adamralph/minver/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/adamralph/minver/actions/workflows/ci.yml?query=branch%3Amain)_
_[![CI package tests](https://github.com/adamralph/minver/actions/workflows/ci-test-packages.yml/badge.svg?branch=main)](https://github.com/adamralph/minver/actions/workflows/ci-test-packages.yml?query=branch%3Amain)_
_[![CodeQL analysis](https://github.com/adamralph/minver/actions/workflows/codeql-analysis.yml/badge.svg?branch=main)](https://github.com/adamralph/minver/actions/workflows/codeql-analysis.yml?query=branch%3Amain)_
_[![InferSharp](https://github.com/adamralph/minver/actions/workflows/infer-sharp.yml/badge.svg?branch=main)](https://github.com/adamralph/minver/actions/workflows/infer-sharp.yml?query=branch%3Amain)_
_[![Lint](https://github.com/adamralph/minver/actions/workflows/lint.yml/badge.svg?branch=main)](https://github.com/adamralph/minver/actions/workflows/lint.yml?query=branch%3Amain)_
_[![Spell check](https://github.com/adamralph/minver/actions/workflows/spell-check.yml/badge.svg?branch=main)](https://github.com/adamralph/minver/actions/workflows/spell-check.yml?query=branch%3Amain)_

A minimalist [.NET package](https://www.nuget.org/packages/MinVer) for versioning .NET SDK-style projects using Git tags.

Platform support: all platforms supported by .NET SDK-style projects.

Also available as a [command-line tool](#can-i-use-minver-to-version-software-which-is-not-built-using-a-net-sdk-style-project) for use in any Git repository.

- [Prerequisites](#prerequisites)
- [Quick start](#quick-start)
- [Usage](#usage)
- [How it works](#how-it-works)
- [Version numbers](#version-numbers)
- [Options](#options)
- [FAQ](#faq)

## Prerequisites

- [.NET SDK 8.0 or later](https://dot.net)
- [Git](https://git-scm.com/)

## Quick start

- Install [MinVer](https://www.nuget.org/packages/MinVer).
- Build your project.

Your project will be versioned according to the latest tag found in the commit history.

**To build with GitHub Actions, [set the fetch depth appropriately](#why-is-the-default-version-sometimes-used-in-github-actions-azure-pipelines-and-travis-ci-when-a-version-tag-exists-in-the-history).**

## Usage

When you want to release a version of your software, whether it's a pre-release, RTM, patch, or anything else, simply create a tag on the commit you want to release with a name which is a valid [SemVer 2.x](https://semver.org/spec/v2.0.0.html) version. For example:

```shell
git tag 1.2.3
git push --tags
```

When you build your software from the commit with that tag, MinVer will apply the version to the assemblies and packages. (If you like to prefix your tag names, see the [FAQ](#can-i-prefix-my-tag-names).)

Bear in mind that MinVer is unaware of the branches which contain the commit, nor your release process, so [it is compatible with any branching or release strategy you choose](#can-i-use-my-own-branching-strategy).

_NOTE: The MinVer package reference should normally include `PrivateAssets="All"`. See [NuGet docs](https://docs.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#controlling-dependency-assets) for more info. If you install MinVer using an IDE or tool, this should be done for you automatically._

## How it works

- If the current commit has a version tag:
  - The version is used as-is.
- If the current commit _does not_ have a version tag:
  - The commit history is searched for the latest commit with a version tag.
    - If a commit with a version tag is found:
      - If the version is a [pre-release](https://semver.org/spec/v2.0.0.html#spec-item-9):
        - The version is used as-is, with [height](#height) added.
      - If the version is RTM (not pre-release):
        - The patch number is incremented, but this [can be customised](#can-i-auto-increment-the-minor-or-major-version-after-an-rtm-tag-instead-of-the-patch-version).
        - Default pre-release identifiers are added. The default identifiers are `alpha.0`, but this [can be customised](#can-i-change-the-default-pre-release-identifiers-from-alpha0-to-something-else).
        - For example, if the latest version tag is `1.0.0`, the current version is `1.0.1-alpha.0`.
        - [Height](#height) is added.
    - If no commit with a version tag is found:
      - The default version `0.0.0-alpha.0` is used, with [height](#height) added.

### Height

If the current commit does not have a version tag, another number is added to the pre-release identifiers. This is the number of commits since the latest commit with a version tag or, if no commits have a version tag, since the root commit. This is known as "height". For example, if the latest version tag found is `1.0.0-beta.1`, at a height of 42 commits, the calculated version is `1.0.0-beta.1.42`.

This behaviour can be [disabled](#can-i-ignore-the-height-of-the-latest-tag-or-root-commit).

For information, see [_What is height for?_](#what-is-height-for)

## Version numbers

MinVer sets the following custom properties:

- `MinVerVersion`
- `MinVerMajor`
- `MinVerMinor`
- `MinVerPatch`
- `MinVerPreRelease`
- `MinVerBuildMetadata`

Those properties are used to set the following .NET SDK properties, satisfying the official [open-source library guidance for version numbers](https://docs.microsoft.com/en-ca/dotnet/standard/library-guidance/versioning#version-numbers):

| Property               | Value                                                                                                           |
|------------------------|-----------------------------------------------------------------------------------------------------------------|
| `AssemblyVersion`      | `{MinVerMajor}.0.0.0`                                                                                           |
| `FileVersion`          | `{MinVerMajor}.{MinVerMinor}.{MinVerPatch}.0`                                                                   |
| `InformationalVersion` | `{MinVerVersion}`                                                                                               |
| `PackageVersion`       | `{MinVerMajor}.{MinVerMinor}.{MinVerPatch}` (or `{MinVerMajor}.{MinVerMinor}.{MinVerPatch}-{MinVerPreRelease}`) |
| `Version`              | `{MinVerMajor}.{MinVerMinor}.{MinVerPatch}` (or `{MinVerMajor}.{MinVerMinor}.{MinVerPatch}-{MinVerPreRelease}`) |

This behaviour can be [customised](#can-i-use-the-version-calculated-by-minver-for-other-purposes).

## Options

Options may be specified as either MSBuild properties (for the [MinVer](https://www.nuget.org/packages/MinVer) package), command-line options (for the [minver-cli](https://www.nuget.org/packages/minver-cli) package), or environment variables (for both the [MinVer](https://www.nuget.org/packages/MinVer) and [minver-cli](https://www.nuget.org/packages/minver-cli) packages).

| MSBuild property or environment variable                                                                                | Command-line option                                                                                                        |
| ----------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| [`MinVerAutoIncrement`](#can-i-auto-increment-the-minor-or-major-version-after-an-rtm-tag-instead-of-the-patch-version) | [`-a\|--auto-increment`](#can-i-auto-increment-the-minor-or-major-version-after-an-rtm-tag-instead-of-the-patch-version)   |
| [`MinVerBuildMetadata`](#can-i-include-build-metadata-in-the-version)                                                   | [`-b\|--build-metadata`](#can-i-include-build-metadata-in-the-version)                                                     |
| [`MinVerDefaultPreReleaseIdentifiers`](#can-i-change-the-default-pre-release-identifiers-from-alpha0-to-something-else) | [`-p\|--default-pre-release-identifiers`](#can-i-change-the-default-pre-release-identifiers-from-alpha0-to-something-else) |
| [`MinVerIgnoreHeight`](#can-i-ignore-the-height-of-the-latest-tag-or-root-commit)                                       | [`-i\|--ignore-height`](#can-i-ignore-the-height-of-the-latest-tag-or-root-commit)                                         |
| [`MinVerMinimumMajorMinor`](#can-i-bump-the-major-or-minor-version)                                                     | [`-m\|--minimum-major-minor`](#can-i-bump-the-major-or-minor-version)                                                      |
| [`MinVerSkip`](#can-i-disable-minver)                                                                                   | n/a (environment variable _not_ supported)                                                                                 |
| [`MinVerTagPrefix`](#can-i-prefix-my-tag-names)                                                                         | [`-t\|--tag-prefix`](#can-i-prefix-my-tag-names)                                                                           |
| [`MinVerVerbosity`](#can-i-get-log-output-to-see-how-minver-calculates-the-version)                                     | [`-v\|--verbosity`](#can-i-get-log-output-to-see-how-minver-calculates-the-version)                                        |
| [`MinVerVersionOverride`](#can-i-use-minver-to-version-software-which-is-not-built-using-a-net-sdk-style-project)       | n/a (environment variable supported)                                                                                       |

Note that the names of the MSBuild properties and environment variables are case-insensitive.

## FAQ

(With TL;DR answers inline.)

<!-- spell-checker:disable -->
- [Why not use GitVersion, Nerdbank.GitVersioning, or some other tool?](#why-not-use-gitversion-nerdbankgitversioning-or-some-other-tool) _(simplicity)_
- [What is height for?](#what-is-height-for) _(not for versioning)_
- [Can I bump the major or minor version?](#can-i-bump-the-major-or-minor-version) _(yes)_
- [Can I use my own pre-release versioning scheme?](#can-i-use-my-own-pre-release-versioning-scheme) _(yes)_
- [Can I prefix my tag names?](#can-i-prefix-my-tag-names) _(yes)_
- [Can I use my own branching strategy?](#can-i-use-my-own-branching-strategy) _(yes)_
- [Can I include build metadata in the version?](#can-i-include-build-metadata-in-the-version) _(yes)_
- [Can I auto-increment the minor or major version after an RTM tag instead of the patch version?](#can-i-auto-increment-the-minor-or-major-version-after-an-rtm-tag-instead-of-the-patch-version) _(yes)_
- [Can I change the default pre-release identifiers from `alpha.0` to something else?](#can-i-change-the-default-pre-release-identifiers-from-alpha0-to-something-else) _(yes)_
- [Can I use the version calculated by MinVer for other purposes?](#can-i-use-the-version-calculated-by-minver-for-other-purposes) _(yes)_
- [Can I version multiple projects in a single repository independently?](#can-i-version-multiple-projects-in-a-single-repository-independently) _(yes)_
- [Can I ignore the height of the latest tag or root commit?](#can-i-ignore-the-height-of-the-latest-tag-or-root-commit) _(yes)_
- [Can I get log output to see how MinVer calculates the version?](#can-i-get-log-output-to-see-how-minver-calculates-the-version) _(yes)_
- [Can I use MinVer to version software which is not built using a .NET SDK style project?](#can-i-use-minver-to-version-software-which-is-not-built-using-a-net-sdk-style-project) _(yes)_
- [Can I disable MinVer?](#can-i-disable-minver) _(yes)_
- [What if the history diverges, and more than one tag or root commit is found?](#what-if-the-history-diverges-and-more-than-one-tag-or-root-commit-is-found) _(nothing bad)_
- [What if the history diverges, and then converges again, before the latest tag (or root commit) is found?](#what-if-the-history-diverges-and-then-converges-again-before-the-latest-tag-or-root-commit-is-found) _(nothing bad)_
- [Why is the default version sometimes used in GitHub Actions, Azure Pipelines, and Travis CI when a version tag exists in the history?](#why-is-the-default-version-sometimes-used-in-github-actions-azure-pipelines-and-travis-ci-when-a-version-tag-exists-in-the-history) _(shallow clones)_
- [Why is my version tag ignored?](#why-is-my-version-tag-ignored) _(MinVer is not running, the tag is misplaced, the version is superseded, the prefix is wrong, or the version is not valid SemVer 2.0)_
<!-- spell-checker:enable -->

### Why not use GitVersion, Nerdbank.GitVersioning, or some other tool?

Before starting MinVer, [Adam Ralph](https://github.com/adamralph) evaluated both [GitVersion](https://github.com/GitTools/GitVersion) and [Nerdbank.GitVersioning](https://github.com/AArnott/Nerdbank.GitVersioning), but neither of them worked in the way he wanted for his projects.

The TL;DR is that MinVer is simpler. ["How it works"](#how-it-works) pretty much captures everything.

#### Comparison with GitVersion

To some degree, MinVer is a subset of what GitVersion is. It's much simpler and doesn't do nearly as much. Some of the differences:

- No dependency on a specific branching pattern.
- No inference of version from branch names.
- No inference of version from YAML config.
- No inference of version from commit messages.
- No inference of version from CI build server environment variables.
- No creation of metadata code artifacts.
- No automatic fetching of tags, etc. from the repository.
- One package instead of a series of packages.
- No support for `AssemblyInfo.cs`.

#### Comparison with Nerdbank.GitVersioning

MinVer is a different approach and, again, simpler. Some of the differences are already listed under the comparison with GitVersion above.

Essentially, Nerdbank.GitVersioning encapsulates the injection of the version into the build process from a config file. That means versions are controlled by commits to that config file. MinVer works purely on tags. That means MinVer doesn't need some of the types of things that come with Nerdbank.GitVersioning such as the config file bootstrapper, and it means the version is controlled independently of the commits. For example, you can tag a commit as a release candidate, build it, and release it. After some time, if the release candidate has no bugs, you can tag the _same commit_ as RTM, build it, and release it.

Also, Nerdbank.GitVersioning uses the Git height for the patch version, which is undesirable. Either _every_ patch commit has to be released, or there will be gaps in the patch versions released.

### What is height for?

[Height](#height) is not for versioning your software. MinVer's design philosophy is _tag first_—you tag the relevant commit before releasing, whether pre-release or RTM. Auto-generated versions (those with height added) are intended only for ad-hoc internal testing, not for release. The presence of height in a version signals that the version should not be released.

Occasionally, requests are made for using height as part of a version, typically for four-part version numbers. Height is inappropriate for this purpose. Following the tag-first philosophy, versions come from tags on the relevant commits, where height is zero. MinVer is designed for [SemVer 2.x](https://semver.org/spec/v2.0.0.html), which provides a three-part semantic version. A fourth version part can only be metadata (such as an incrementing build number). See [_Can I use the version calculated by MinVer for other purposes?_](#can-i-use-the-version-calculated-by-minver-for-other-purposes) for examples.

### Can I bump the major or minor version?

Yes! You probably want to do this because at a point in time, on a given branch, you are working on a specific `MAJOR.MINOR` range, e.g. `1.0`, `1.1`, or `2.0`. The branch could be `main`, `develop`, a special release branch, a support branch, or anything else.

Before you create the first version tag on your branch, interim builds will use the latest version tag found in the commit history, which may not match the `MAJOR.MINOR` range you are working on. Or if no version tag is found in the commit history, interim builds will have the default version `0.0.0-alpha.0`. If you prefer those interim builds to have a version in the range you are working on, you have two options:

#### Tag a commit

Tag a commit in your branch with a version matching your `MAJOR.MINOR` range, using your [preferred default pre-release identifiers](#can-i-change-the-default-pre-release-identifiers-from-alpha0-to-something-else). For example:

```shell
git tag 1.0.0-alpha.0
```

This is not a version you will release, since the first "alpha" version will be `1.0.0-alpha.1`. The only purpose of this tag is to force MinVer to start versioning commits in your branch in the `1.0` range.

If you begin to release versions in the `1.0` range from another branch (e.g. a special release branch), tag a commit in your branch with `1.1.0-alpha.0`, `2.0.0-alpha.0`, or whatever `MAJOR.MINOR` range your branch now represents.

#### Set MinVerMinimumMajorMinor

Specify your range with [`MinVerMinimumMajorMinor`](#options). For example:

```xml
<PropertyGroup>
  <MinVerMinimumMajorMinor>1.0</MinVerMinimumMajorMinor>
</PropertyGroup>
```

MinVer will now use a default version of `1.0.0-alpha.0`.

If you begin to release versions in the `1.0` range from another branch (e.g. a special release branch), set MinVerMinimumMajorMinor to `1.1`, `2.0`, or whatever `MAJOR.MINOR` range your branch now represents.

Note that `MinVerMinimumMajorMinor` will be redundant after you create the first tag in your branch with same `MAJOR.MINOR`. If you don't care that the versions of interim builds before that first tag will have a lower `MAJOR.MINOR`, then simply don't specify `MinVerMinimumMajorMinor`.

Also note that if the latest version tag found in the commit history has a higher `MAJOR.MINOR` than `MinVerMinimumMajorMinor`, then `MinVerMinimumMajorMinor` will be ignored.

Lastly, if the the current commit has a version tag, the tag will be used as-is, even if it has a lower `MAJOR.MINOR` than `MinVerMinimumMajorMinor`.

### Can I use my own pre-release versioning scheme?

Yes! MinVer doesn't care what your pre-release versioning scheme is. The default pre-release identifiers are `alpha.0`, but you can use whatever you like in your tags. If your versioning scheme is valid [SemVer 2.x](https://semver.org/spec/v2.0.0.html), it will work with MinVer.

For example, all these versions work with MinVer:

- `1.0.0-beta.1`
- `1.0.0-pre.1`
- `1.0.0-preview-20181104`
- `1.0.0-rc.1`

### Can I prefix my tag names?

Yes! Specify the prefix with [`MinVerTagPrefix`](#options).

For example, if you prefix your tag names with "v", e.g. `v1.2.3`:

```xml
<PropertyGroup>
  <MinVerTagPrefix>v</MinVerTagPrefix>
</PropertyGroup>
```

Note that the prefix is case-insensitive—in this example, both `v1.2.3` and `V1.2.3` would work.

### Can I use my own branching strategy?

Yes! MinVer doesn't care about branches. It's all about the tags!

That means MinVer is compatible with [Git Flow](https://nvie.com/posts/a-successful-git-branching-model/), [GitHub Flow](https://guides.github.com/introduction/flow/), [Release Flow](https://devblogs.microsoft.com/devops/release-flow-how-we-do-branching-on-the-vsts-team/), and any other exotic flow.

### Can I include build metadata in the version?

Yes! Specify [build metadata](https://semver.org/spec/v2.0.0.html#spec-item-10) with `MinVerBuildMetadata`.

For example, in a GitHub Actions workflow:

```yaml
env:
  MINVERBUILDMETADATA: build.${{ github.run_id }}.${{ github.run_attempt}}
```

You can also specify build metadata in a version tag. If the tag is on the current commit, its build metadata will be used. If the tag is on an older commit, its build metadata will be ignored. Build metadata in `MinVerBuildMetadata` will be appended to build metadata in the tag.

Build metadata is only included in the [assembly informational version](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning#assembly-informational-version). You can include it elsewhere using a custom target. E.g.:

```xml
<Target Name="MyTarget" AfterTargets="MinVer" Condition="'$(MinVerBuildMetadata)' != ''" >
  <PropertyGroup>
    <PackageVersion>$(PackageVersion)+$(MinVerBuildMetadata)</PackageVersion>
    <Version>$(PackageVersion)</Version>
  </PropertyGroup>
</Target>
```

### Can I auto-increment the minor or major version after an RTM tag instead of the patch version?

Yes! Specify which part of the version to auto-increment with `MinVerAutoIncrement`. By default, [MinVer will auto-increment the patch version](#how-it-works), but you can specify `minor` or `major` to increment the minor or major version instead.

### Can I change the default pre-release identifiers from `alpha.0` to something else?

Yes! Specify the default pre-release identifiers with `MinVerDefaultPreReleaseIdentifiers`. For example, if you prefer to name your pre-releases as "preview":

```xml
<PropertyGroup>
  <MinVerDefaultPreReleaseIdentifiers>preview.0</MinVerDefaultPreReleaseIdentifiers>
</PropertyGroup>
```

This will result in a post-RTM version of `{major}.{minor}.{patch+1}-preview.0.{height}`, e.g. `1.0.1-preview.0.1`.

Note that the numeric part(s) of the default pre-release identifiers should usually be `0` (meaning "alpha 0", "preview 0", etc.), since any versions produced between an RTM tag and the next tag are not tagged for release, they are just versions produced between the RTM and the next RTM or pre-release. When you tag your first pre-release, you will typically increment the `0` to `1`, meaning "alpha 1", "preview 1", etc.

### Can I use the version calculated by MinVer for other purposes?

Yes! You can use any of the [properties set by MinVer](#version-numbers), or override their values, in a target which runs after MinVer.

For example, for projects which do not create NuGet packages, you may want to populate [all four parts](https://docs.microsoft.com/en-us/dotnet/framework/app-domains/assembly-versioning#assembly-version-number) of `AssemblyVersion`. E.g. using GitHub Actions:

```xml
<Target Name="MyTarget" AfterTargets="MinVer">
  <PropertyGroup>
    <GITHUB_RUN_NUMBER Condition="'$(GITHUB_RUN_NUMBER)' == ''">0</GITHUB_RUN_NUMBER>
    <AssemblyVersion>$(MinVerMajor).$(MinVerMinor).$(GITHUB_RUN_NUMBER).$(MinVerPatch)</AssemblyVersion>
  </PropertyGroup>
</Target>
```

Or for projects which _do_ create NuGet packages, you may want to adjust the assembly file version to include the build number, as recommended in the [official guidance](https://docs.microsoft.com/en-ca/dotnet/standard/library-guidance/versioning#assembly-file-version). E.g. when using GitHub Actions:

```xml
<Target Name="MyTarget" AfterTargets="MinVer">
  <PropertyGroup>
    <GITHUB_RUN_NUMBER Condition="'$(GITHUB_RUN_NUMBER)' == ''">0</GITHUB_RUN_NUMBER>
    <FileVersion>$(MinVerMajor).$(MinVerMinor).$(MinVerPatch).$(GITHUB_RUN_NUMBER)</FileVersion>
  </PropertyGroup>
</Target>
```

### Can I version multiple projects in a single repository independently?

Yes! You can do this by using a specific tag prefix for each project. For example, if you have a "main" project and an "extension" project, you could specify `<MinVerTagPrefix>main-</MinVerTagPrefix>` in the main project and `<MinVerTagPrefix>ext-</MinVerTagPrefix>` in the extension project. To release version `1.0.0` of the main project you'd tag the repository with `main-1.0.0`. To release version `1.1.0` of the extension project you'd tag the repository with `ext-1.1.0`.

Note that changes committed to a given project will affect the versions of all other projects because every commit affects [height](#height). To prevent this, you can [ignore the height of the latest tag or root commit](#can-i-ignore-the-height-of-the-latest-tag-or-root-commit).

### Can I ignore the height of the latest tag or root commit?

Yes! Set [`MinVerIgnoreHeight`](#options) to `true`:

```xml
<PropertyGroup>
  <MinVerIgnoreHeight>true</MinVerIgnoreHeight>
</PropertyGroup>
```

This is often useful if you are [versioning multiple projects in a single repository independently](#can-i-version-multiple-projects-in-a-single-repository-independently).

### Can I get log output to see how MinVer calculates the version?

Yes! [`MinVerVerbosity`](#options) may be set to:

- `quiet`
- `minimal` (default)
- `normal`
- `detailed`
- `diagnostic`.

These verbosity levels match those in MSBuild and therefore `dotnet build`, `dotnet pack`, etc. The default is `minimal`, which matches the default in MSBuild.

The equivalent levels for the [minver-cli](https://www.nuget.org/packages/minver-cli) `-v|--verbosity` option are:

<!-- spell-checker:disable -->
- `e[rror]`
- `w[arn]`
- `i[nfo]` (default)
- `d[ebug]`
- `t[race]`
<!-- spell-checker:enable -->

These verbosity levels match those typically used in console applications.

At the `quiet`/`error` and `minimal`/`warn` levels, you will see only warnings and errors. At the `normal`/`info` level you will see which commit is being used to calculate the version, and the calculated version. At the `detailed`/`debug` level you will see how many commits were examined, which version tags were found but ignored, which version was calculated, etc. At the `diagnostic`/`trace` level you will see how MinVer walks the commit history, in excruciating detail.

In a future version of MinVer, the verbosity level may be inherited from MSBuild, in which case `MinVerVerbosity` will be deprecated.

### Can I use MinVer to version software which is not built using a .NET SDK style project?

Yes! MinVer is also available as a [command-line tool](https://www.nuget.org/packages/minver-cli). Run `minver --help` for usage. The calculated version is printed to standard output (stdout).

Sometimes you may want to version both .NET projects and other outputs, such as non-.NET projects, or a container image, in the same build. In those scenarios, you should use both the command-line tool _and_ the regular MinVer package. Before building any .NET projects, your build script should run the command-line tool and set the [`MINVERVERSIONOVERRIDE`](#options) environment variable to the calculated version. The MinVer package will then use that value rather than calculating the version a second time. This ensures that the command-line tool and the MinVer package produce the same version.

If the components of the version are required outside the context of a .NET project, they can easily be extracted from the version which is returned by the command-line tool. The major, minor, and patch numbers can be separated from the pre-release identifiers by splitting the version by the first instance of `-`. The major, minor, and patch numbers and the pre-release identifiers can then be separated by splitting by `.`.

### Can I disable MinVer?

Yes! Set [`MinVerSkip`](#options) to `true`. For example, MinVer can be disabled for debug builds:

```xml
<PropertyGroup>
  <MinVerSkip Condition="'$(Configuration)' == 'Debug'">true</MinVerSkip>
</PropertyGroup>
```

### What if the history diverges, and more than one tag or root commit is found?

MinVer will use the tag with the higher version, or the tag or root commit on the first path followed where the history diverges. The paths are followed in the same order that the parents of the commit are stored in Git. The first parent is the commit on the branch that was the current branch when the merge was performed. The remaining parents are stored in the order that their branches were specified in the merge command.

### What if the history diverges, and then converges again, before the latest tag (or root commit) is found?

MinVer will use the height on the first path followed where the history diverges. The paths are followed in the same order that the parents of the commit are stored in Git. The first parent is the commit on the branch that was the current branch when the merge was performed. The remaining parents are stored in the order that their branches were specified in the merge command.

### Why is the default version sometimes used in GitHub Actions, Azure Pipelines and Travis CI when a version tag exists in the history?

By default, [GitHub Actions](https://github.com/features/actions/), [Azure Pipelines](https://azure.microsoft.com/en-us/products/devops/pipelines), and [Travis CI](https://travis-ci.org/) use [shallow clones](https://www.git-scm.com/docs/git-clone#Documentation/git-clone.txt---depthltdepthgt). The GitHub Actions [checkout action](https://github.com/actions/checkout) and Azure Pipelines default [checkout step](https://learn.microsoft.com/en-us/azure/devops/pipelines/yaml-schema/steps-checkout) both clone with a depth of only a single commit, and Travis CI clones with a depth of 50 commits. In GitHub Actions and Azure Pipelines, if the latest version tag in the history is not on the current commit, it will not be found. In Travis CI, if the latest version tag in the history is at a height of more than 50 commits, it will not be found.

To build in GitHub Actions, Azure Pipelines, or Travis CI, configure them to fetch a sufficient number of commits.

For GitHub Actions, set the `fetch-depth` of the [checkout action](https://github.com/actions/checkout) to an appropriate number, or to zero for all commits (you can also set `filter` to `tree:0` to create a [treeless clone](https://github.blog/2020-12-21-get-up-to-speed-with-partial-clone-and-shallow-clone/), for better performance):

```yaml
- uses: actions/checkout@v4
  with:
    fetch-depth: 0
    filter: tree:0
```

For Azure Pipelines, include an explicit [checkout step](https://learn.microsoft.com/en-us/azure/devops/pipelines/yaml-schema/steps-checkout) and set the `fetchDepth` to an appropriate number, or to zero for all commits (you can also set `fetchFilter` to `tree:0` to create a [treeless clone](https://github.blog/2020-12-21-get-up-to-speed-with-partial-clone-and-shallow-clone/), for better performance):

```yaml
steps:
  - checkout: self
    fetchDepth: 0
    fetchFilter: tree:0
```

For Travis CI, set the [`--depth` flag](https://docs.travis-ci.com/user/customizing-the-build#git-clone-depth) to an appropriate number, or to `false` for all commits:

```yaml
git:
  depth: false
```

### Why is my version tag ignored?

Things to check:

- MinVer is running:
  - Is [`MinVerSkip`](#can-i-disable-minver) set?
- The tag is in the correct place:
  - Is the tag on the current commit or on an ancestor of the current commit?
- The version is not superseded:
  - Is there a version tag on a later ancestor of the current commit?
  - Is there a version tag with a later version on the same commit?
  - Is [`MinVerVersionOverride`](#can-i-use-minver-to-version-software-which-is-not-built-using-a-net-sdk-style-project) set?
  - Is [`MinVerMinimumMajorMinor`](#can-i-bump-the-major-or-minor-version) set to a higher major and minor?
- The correct prefix is used:
  - Does the tag prefix match [`MinVerTagPrefix`](#can-i-prefix-my-tag-names)?
- The version is valid [SemVer 2.0](https://semver.org/spec/v2.0.0.html):
  - Are the major, minor, and patch non-negative integers?
  - Do the major, minor, patch, or numeric pre-release identifiers have leading zeros?
  - Do the pre-release or build metadata identifiers contain anything other than ASCII alphanumerics and hyphens [0-9A-Za-z-]?
  - Is any pre-release or build metadata identifier empty (two or more dots in a row)?
  - Is there more than one plus sign?

If you still can't figure it out, increase [`MinVerVerbosity`](#can-i-get-log-output-to-see-how-minver-calculates-the-version) to `detailed` to see which tags are explicitly ignored.

---

<sub>[Tag](https://thenounproject.com/term/tag/938952) by [Ananth](https://thenounproject.com/ananthshas/) from [the Noun Project](https://thenounproject.com/).</sub>
