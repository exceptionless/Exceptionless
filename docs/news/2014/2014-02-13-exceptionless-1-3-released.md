---
title: "Exceptionless 1.3 Released"
date: 2014-02-13
---

# Exceptionless 1.3 Released

Exceptionless 1.3 brings with it both server and client changes, including [open sourcing the project](/fork-us-exceptionless-goes-open-source/ "Exceptionless Goes Open Source!") (which we're super excited about!), some minor updates, and a few bug fixes. Check out the changelog items below, and let us know if you have any questions.

## Server

* Open sourced the server under the [GNU Affero General Public License, Version 3.0](http://www.gnu.org/licenses/agpl-3.0.html)!
* Extended data key names are now shown with friendly formatted name.
* Summary notification emails that were not sent out and are older than two days will be ignored by the email job. This will help prevent users from being spammed in some circumstances.
* Fixed a major application start up performance bug that had to do with dependency injection.
* Fixed a bug that was causing the key-up event to be cancelled on every page that had a chart. This affected modals from working properly in some scenarios.
* The following improvements were made specifically to make local development and internal deployments easier:
  * Added the ability to add or remove a user from the admin role.
  * When you are running exceptionless in development mode and you are the first user to signup for an account, a new sample project with a sample api key will be created.
  * When billing is not configured, all new organizations will be placed in an unlimited plan.

## Client

* Open sourced the client under the [Apache License, Version 2.0](http://www.apache.org/licenses/LICENSE-2.0)!
* Symbols are now available on [http://www.symbolsource.org/](http://www.symbolsource.org/). Documentation on configuring visual studio can be found [here](http://www.symbolsource.org/Public/Home/VisualStudio).
* Adding binding redirects to the right version.
* Data Exclusions now [support wildcards](/docs/security).