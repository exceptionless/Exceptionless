# Contributing to Exceptionless

## Before You Start

Anyone wishing to contribute to the **[Exceptionless/Exceptionless](https://github.com/exceptionless/exceptionless)** project **MUST read &amp; sign the [Electronic Exceptionless Contribution License Agreement](http://exceptionless.clahub.com)**. The Exceptionless team is legally prevented from accepting any pull requests from users who have not signed the CLA first.

## Reporting Bugs

1. Always update to the most recent master release; the bug may already be resolved.

2. Search for similar issues on the [Exceptionless uservoice forum][m]; it may already be an identified problem.

3. If this is a bug or problem that **requires any kind of extended discussion -- open [a topic on uservoice][m] about it**. Do *not* open a bug on GitHub.

4. If this is a bug or problem that is clear, simple, and is unlikely to require *any* discussion -- it is OK to open an [issue on GitHub](https://github.com/exceptionless/exceptionless/issues) with a reproduction of the bug including workflows, screenshots, or links to examples. If possible, submit a Pull Request with a failing test. If you'd rather take matters into your own hands, fix the bug yourself (jump down to the "Contributing (Step-by-step)" section).
5. When the bug is fixed, we will do our best to update the Exceptionless topic or GitHub issue with a resolution.

## Requesting New Features

1. Do not submit a feature request on GitHub; all feature requests on GitHub will be closed. Instead, visit the **[Exceptionless uservoice forum][m]**, and search this list for similar feature requests. It's possible somebody has already asked for this feature or provided a pull request that we're still discussing.

2. Provide a clear and detailed explanation of the feature you want and why it's important to add. The feature must apply to a wide array of users of Exceptionless. You may also want to provide us with some advance documentation on the feature, which will help the community to better understand where it will fit.

3. If you're a Rock Star programmer, build the feature yourself (refer to the "Contributing (Step-by-step)" section below).

## Contributing (Step-by-step)

1. Refer to the GitHub documentation on how to fork the repository:

    https://help.github.com/articles/fork-a-repo

2. Follow the Coding Conventions
  * Adhere to common conventions you see in the existing code
  * Include tests, and ensure they pass
  * Search to see if your new functionality has been discussed on [the Exceptionless uservoice forum](http://exceptionless.uservoice.com), and include updates as appropriate
  * four spaces, no tabs
  * no trailing whitespaces, blank lines should have no spaces
  * use spaces around operators, after commas, colons, semicolons, around `{` and before `}`
  * no space after `(`, `[` or before `]`, `)`

  > However, please note that **pull requests consisting entirely of style changes are not welcome on this project**. Style changes in the context of pull requests that also refactor code, fix bugs, improve functionality *are* welcome.

3. Issue a Pull Request

    https://help.github.com/articles/using-pull-requests
  
  Thanks for that -- we'll get to your pull request ASAP, we love pull requests!

10. Responding to Feedback

  The Exceptionless team may recommend adjustments to your code. Part of interacting with a healthy open-source community requires you to be open to learning new techniques and strategies; *don't get discouraged!* Remember: if the Exceptionless team suggest changes to your code, **they care enough about your work that they want to include it**, and hope that you can assist by implementing those revisions on your own.
  
[m]: https://exceptionless.uservoice.com
