# Exceptionless.UI
[![Build status](https://ci.appveyor.com/api/projects/status/18th2gqmbt86p5y0/branch/master?svg=true)](https://ci.appveyor.com/project/Exceptionless/exceptionless-ui)
[![Slack Status](https://slack.exceptionless.com/badge.svg)](https://slack.exceptionless.com)
[![Donate](https://img.shields.io/badge/donorbox-donate-blue.svg)](https://donorbox.org/exceptionless)

Exceptionless User Interface

## Using Exceptionless
Refer to the [Exceptionless documentation wiki](https://github.com/exceptionless/Exceptionless/wiki/Getting-Started).

## Hosting Options
We provide very reasonably priced hosting at [Exceptionless](https://exceptionless.com). By using our hosted service, you are supporting the project and helping it get better! We also provide set up and support services.

If you would rather host Exceptionless yourself, you will need to follow the [self hosting documentation](https://github.com/exceptionless/Exceptionless/wiki/Self-Hosting).

## Contributing
_In appreciation for anyone who submits a non-trivial pull request, we will give you a free [Exceptionless](https://exceptionless.io) paid plan for a year. After your pull request is accepted, simply send an email to team@exceptionless.io with the name of your organization and we will upgrade you to a paid plan._

Please read the [contributing document](https://github.com/exceptionless/Exceptionless/blob/master/CONTRIBUTING.md) and follow the steps below to start configuring your Exceptionless development environment.

1. You will need to clone this repo.
2. Install [grunt](https://gruntjs.com/) and the development dependencies using [npm](https://www.npmjs.com/).

   ```javascript
   npm install
   ```
3. Download the JavaScript dependencies by running the following [bower](https://bower.io/) command.

   ```javascript
   npx bower install
   ```
4. Start a web server and view it on [`http://ex-ui.localtest.me:5100`](http://ex-ui.localtest.me:5100) by running the following grunt command.

   ```javascript
   npx grunt serve
   ```
