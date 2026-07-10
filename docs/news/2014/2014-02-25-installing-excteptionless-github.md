---
title: "Installing Exceptionless from GitHub"
date: 2014-02-25
---

# Installing Exceptionless from GitHub

Since we officially announced that Exceptionless was going open source last week, we wanted to provide everyone with a quick and easy video walkthrough of how to get up and running locally.

It's really quick, as you can see from the below video. Below the video is also a textual walkthrough. Please take a look and let us know if you have any questions.

Please note that before contributing to the Exceptionless project, you must read and sign the Exceptionless [Contribution License Agreement](http://www.clahub.com/agreements/exceptionless/Exceptionless). Pull requests will not be accepted otherwise.

### GitHub Exceptionless Getting Started Video

https://www.youtube.com/watch?v=wROzlVuBoDs

### Text Guide

1. Log in to github
2. Install the [GitHub Windows client](https://windows.github.com/), if you want to use the GUI. If not, you can do the rest of the steps from command line.
3. Fork the [Exceptionless repository](https://github.com/exceptionless/Exceptionless)
4. Clone the repo to your machine (Clone to Desktop)
5. Open your local repository you just cloned
6. Follow the "[Getting Started](https://github.com/exceptionless/Exceptionless#getting-started)" section of github readme.
7. Start StartBackendServers.bat file to start redis and mongodb
8. Open the Exceptionles solution in Visual Studio
9. Right click solution and select select "set startup projects"
10. Click on "Multiple startup projects"
11. Locate Exceptionless.app and Exceptionless.SampleConsole and change them to "Start"
12. Rebuild the solution to pull down the NuGet packages
13. Start Debugging
14. A console app and Internet explorer instance will start
15. Go to the browser and create a (sample) account. This will create a sample organization and project.
16. You will be redirected to the dashboard for the new project
17. Go back to the console app and hit 1. A new error will be generated and your Exceptionless dashboard should reflect the error in real-time.
18. Now, after you make any changes or updates, you will want to do a pull request.
19. To do so, commit working, tested changes to the project.
20. Then, sync the changes
21. Go back to github and click on the green compare, review, or create a pull request icon.
22. Review the updates and make sure the pull request includes the proper changes.
23. Click "Create a Pull Request"
24. Add any comments relevant to the pull request. Details are great!
25. Click "Send Pull Request"
26. The Exceptionless Team will review the request and merge it into the project, provide feedback, etc.

Please let us know if you have any questions. Happy coding!
