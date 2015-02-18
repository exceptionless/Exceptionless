# Wintellect PowerShell Change Log #

## December 27, 2014
Small updates

- Added Set-Signatures to make signing scripts easier mainly for me and others who sign but also have Azure certificates on the computer.
- Removed unneeded array conversion in Compare-Directories
- Removed the -Quiet switch to Add-NgenPdbs and removed Write-Host calls in the function. Use the standard -Verbose to see the output.

## November 11, 2014
A huge refactor! 

- Full support for the spanking new Visual Studio 2015.
- Now dot sourcing the individual files so I can start sharing some code.
- Fixed many of the warnings reported by Script Analyer 1.4.
- Now requiring PowerShell 4.0.
- Removed my Get-Hash and now using Get-FileHash.
- Remove-IntelliTraceFiles now supports -Latest like Import-VisualStudioEnvironment
- Get-SysInternalsSuite uses Invoke-WebRequest and additionally called Unblock-File to unlock all the extracted files.
- Fixed the issue where I was not exporting the set alias correctly.


## July 7, 2014
Fixed an issue in Add-NgenPdbs where I wasn't handling the case where the VS cache directory could be blank.

## June 18, 2014
Fixed an issue reported by [Chris Fraire/idodeclare](https://github.com/idodeclare) is Set-ProjectProperties where the assembly name for XML document comments was not set correctly in all cases.

Ensured Set-ProjectProperties.ps1 is clean as reported by Microsoft's Script Analyzer plug in.

## June 13, 2014
Fixed an issue with relative paths in Expand-ZipFile

Added the -CurrentEnvironmentOnly  switch to both Set-SymbolServer and Set-SourceServer that changes on the environment variables for the current PowerShell window. This is only useful when using WinDBG because Visual Studio requires registry settings instead of environment variables.

For the two files I touched, ensured they are clean as reported by Microsoft's Script Analyzer plug in.

## March 11, 2014
Fixed a bug in Compare-Directories.

## November 18, 2013
Fixed a copy pasta bug in Get-SourceServerFiles

Add-NgenPdbs now properly supports VS 2013

Re-digitally signed everything with my new code signing certificate as the old one was expiring. I didn't have to do that but was signing the changed file so went ahead and did all of them.

## July 17, 2013
Updated Set-SymbolServer, Set-SourceServer, Get-SourceServer, and Remove-IntelliTraceFiles to support VS 2013.

## June 24, 2013
Fixed a bug in Import-VisuaStudioEnvironment where I should be looking at the Wow6432Node on x64 dev boxes reported by [RaHe67](https://github.com/RaHe67). Also updated the cmdlet with the official version and name of VS 2013.

## May 20, 2013
Updated the Set-SymbolServer -public switch to use the same cache on both the reference source and msdl download items. With VS 2012 this works better and helps avoid multiple downloads of various PDB files. Since I no longer use VS 2010, I'm not sure what affect this will have on that version. Also, I turn off using the Microsoft symbol servers as I'm putting them all in the _NT_SYMBOL_PATH environment variable anyway.

Additionally, Set-SymbolServer now puts any specified symbol servers with the -SymbolServers switch at the front of the _NT_SYMBOL_PATH environment variable. This will make symbol downloading faster for those with your own symbol server set up.

## May 9, 2013
Added the Set-Environment, Invoke-CmdScript, and Import-VisuaStudioEnvironment cmdlets.

The Invoke-CmdScript cmdlet is based off [Lee Holmes'](http://www.leeholmes.com/blog/2006/05/11/nothing-solves-everything-%e2%80%93-powershell-and-other-technologies/) version.

The Set-Environment cmdlet is from [Wes Haggard](http://weblogs.asp.net/whaggard/archive/2007/02/08/powershell-version-of-cmd-set.aspx). To replace the default set alias with the one provided by WintellectPowerShell, execute the following command before importing the module:

`Import-Module WintellectPowerShell
Remove-Item alias:set -Force -ErrorAction SilentlyContinue
`

## February 25, 2013
Added the Add-NgenPdb cmdlet.

## January, 27, 2013
Added the very cool Set-ProjectProperties cmdlet to make batch updating of Visual Studio projects much easier. Right now it only supports C# projects.

Changed the architecture of the whole module to break up a single .PSM1 file into different files for each cmdlet. This will make development much easier going forward.

Removed the external help XML file and put all help back into the source code. Editing the external file was a pain in the butt because the editor leaves lots to be desired and I was never going to support updatable help anyway.

## October 14, 2012 ##
Added the following cmdlets:
Compare-Directories - Can compare directories to see if they contain the same filenames as well as the same content.

Get-Hash - Gets the cryptographic hash for a file or string.

## September 29, 2012 ##
Added the following cmdlets:

Test-RegPath - Original author [Can Dedeoglu](http://blogs.msdn.com/candede "Can Dedeoglu")

Remove-IntelliTraceFiles - If saving your debugging IntelliTrace files, the directory can quickly fill with many large files. This cmdlet keeps your IntelliTrace file directory cleaned up.

## August 29, 2012 ##
Initial release to GitHub.