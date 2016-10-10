Exceptionless self hosted installation

Please follow the steps below or watch the following video to get started: https://www.youtube.com/watch?v=P01v289dR10

## Requirements

.NET 4.6
Java 1.8+ (The JAVA_HOME environment variable must also be configured when using Windows.)
IIS Express 8+
PowerShell 3+

## Instructions

1. Please ensure that an PowerShell execution policy has been set.
  a. Open command prompt as an administrator.
  b. Enter powershell Set-ExecutionPolicy Unrestricted and press enter.
2. Double click on c:\exceptionless\Start.bat. This will automatically launch ElasticSearch, IIS Express, and your default browser to the exceptionless website.

For more detailed instructions and how to configure Exceptionless for a production environment please
read our self hosting documentation ( https://github.com/exceptionless/Exceptionless/wiki/Self-Hosting ).
