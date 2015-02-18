#requires -version 2.0
###############################################################################
# WintellectPowerShell Module
# Copyright (c) 2010-2014 - John Robbins/Wintellect
# 
# Do whatever you want with this module, but please do give credit.
###############################################################################

# Always make sure all variables are defined and all best practices are 
# followed.
Set-StrictMode -version Latest

###############################################################################
# Script Global Variables
###############################################################################
$script:devTenDebuggerRegKey = "HKCU:\Software\Microsoft\VisualStudio\10.0\Debugger"
$script:devElevenDebuggerRegKey = "HKCU:\Software\Microsoft\VisualStudio\11.0\Debugger"
$script:devTwelveDebuggerRegKey = "HKCU:\Software\Microsoft\VisualStudio\12.0\Debugger"
$script:devFourteenDebuggerRegKey = "HKCU:\Software\Microsoft\VisualStudio\14.0\Debugger"

###############################################################################
# Module Only Functions
###############################################################################
function CreateDirectoryIfNeeded ( [string] $directory )
{
	if ( ! ( Test-Path -Path $directory -type "Container" ) )
	{
		New-Item -type directory -Path $directory > $null
	}
}

# Reads the values from VS 2010+, and the environment.
function GetCommonSettings($regValue, $envVariable)
{
    $returnHash = @{}
    if (Test-Path -Path $devTenDebuggerRegKey)
    {
        $returnHash["VS 2010"] = 
                (Get-ItemProperty -Path $devTenDebuggerRegKey).$regValue
    }
    if (Test-Path -Path $devElevenDebuggerRegKey)
    {
        $returnHash["VS 2012"] = 
                (Get-ItemProperty -Path $devElevenDebuggerRegKey).$regValue
    }
    if (Test-Path -Path $devTwelveDebuggerRegKey)
    {
        $returnHash["VS 2013"] = 
                (Get-ItemProperty -Path $devTwelveDebuggerRegKey).$regValue
    }
    if (Test-Path -Path $devFourteenDebuggerRegKey)
    {
        $returnHash["VS 2015"] = 
                (Get-ItemProperty -Path $devFourteenDebuggerRegKey).$regValue
    }
    $envVal = Get-ItemProperty -Path HKCU:\Environment -Name $envVariable -ErrorAction SilentlyContinue
    if ($envVal -ne $null)
    {
        $returnHash[$envVariable] = $envVal.$envVariable
    }
    $returnHash
}

# Makes doing ShouldProcess easier.
function Set-ItemPropertyScript ( $path , $name , $value , $type )
{
    if ( $path -eq $null )
    {
        throw "Set-ItemPropertyScript path param cannot be null!"
    }
    if ( $name -eq $null )
    {
        throw "Set-ItemPropertyScript name param cannot be null!"
    }
	$propString = "Item: " + $path.ToString() + " Property: " + $name
	if ($PSCmdLet.ShouldProcess($propString ,"Set Property"))
	{
        if ($type -eq $null)
        {
		  Set-ItemProperty -Path $path -Name $name -Value $value
        }
        else
        {
		  Set-ItemProperty -Path $path -Name $name -Value $value -Type $type
        }
	}
}

function SetInternalSymbolServer([string] $DbgRegKey , 
                                 [string] $CacheDirectory ,
                                 [string] $SymPath )
{

    CreateDirectoryIfNeeded -directory $CacheDirectory
    
    # Turn off Just My Code.
    Set-ItemPropertyScript $dbgRegKey JustMyCode 0 DWORD

    # Turn off .NET Framework Source stepping.
    Set-ItemPropertyScript $DbgRegKey FrameworkSourceStepping 0 DWORD

    # Turn off using the Microsoft symbol servers.
    Set-ItemPropertyScript $DbgRegKey SymbolUseMSSymbolServers 0 DWORD

    # Set the symbol cache dir to the same value as used in the environment
    # variable.
    Set-ItemPropertyScript $DbgRegKey SymbolCacheDir $CacheDirectory
} 

function SetPublicSymbolServer([string] $DbgRegKey , 
                               [string] $CacheDirectory )
{
    CreateDirectoryIfNeeded -directory $CacheDirectory
        
    # Turn off Just My Code.
    Set-ItemPropertyScript $dbgRegKey JustMyCode 0 DWORD
    
    # Turn on .NET Framework Source stepping.
    Set-ItemPropertyScript $dbgRegKey FrameworkSourceStepping 1 DWORD
    
    # Turn on Source Server Support.
    Set-ItemPropertyScript $dbgRegKey UseSourceServer 1 DWORD
    
    # Turn on Source Server Diagnostics as that's a good thing. :)
    Set-ItemPropertyScript $dbgRegKey ShowSourceServerDiagnostics 1 DWORD
    
    # It's very important to turn off requiring the source to match exactly.
    # With this flag on, .NET Reference Source Stepping doesn't work.
    Set-ItemPropertyScript $dbgRegKey UseDocumentChecksum 0 DWORD
    
    # Turn off using the Microsoft symbol servers. 
    Set-ItemPropertyScript $dbgRegKey SymbolUseMSSymbolServers 0 DWORD
    
    # Set the VS SymbolPath setting.
    Set-ItemPropertyScript $dbgRegKey SymbolPath ""
    
    # Tell VS that all paths are empty.
    Set-ItemPropertyScript $dbgRegKey SymbolPathState ""
    
    # Set the symbol cache dir to the same value as used in the environment
    # variable.
    Set-ItemPropertyScript $dbgRegKey SymbolCacheDir $CacheDirectory
}

###############################################################################
# Public Cmdlets
###############################################################################
function Get-SourceServer
{
<#
.SYNOPSIS
Returns a hashtable of the current source server settings.

.DESCRIPTION
Returns a hashtable with the current source server directories settings
for VS 2010-2015, and the _NT_SOURCE_PATH enviroment variable 
used by WinDBG.

.OUTPUTS 
HashTable
The keys are, if all present, VS 2010, VS 2012, VS 2013, VS 2015, and WinDBG. 
The  values are those set for each debugger.

.LINK
http://www.wintellect.com/blogs/jrobbins
https://github.com/Wintellect/WintellectPowerShell

#>
    GetCommonSettings SourceServerExtractToDirectory _NT_SOURCE_PATH
}

###############################################################################

function Set-SourceServer
{
<#
.SYNOPSIS
Sets the source server directory.

.DESCRIPTION
Sets the source server cache directory for VS 2010, VS 2012, VS 2013, VS 2015, 
and WinDBG  through the _NT_SOURCE_PATH environment variable to all reference 
the same location. This ensures you only download the file once no matter 
which debugger you use. Because this cmdlet sets an environment variable 
you need to log off to ensure it's properly set.

.PARAMETER Directory
The directory to use. If the directory does not exist, it will be created.

.PARAMETER CurrentEnvironmentOnly
If specified will only set the current PowerShell window _NT_SOURCE_PATH 
environment variable and not overwrite the global settings. This is primarily
for use with WinDBG as Visual Studio does not use this environment variable.

.LINK
http://www.wintellect.com/blogs/jrobbins
https://github.com/Wintellect/WintellectPowerShell
#>
    param ( 
        [Parameter(Mandatory=$true,
                   HelpMessage="Please specify the source server cache directory")]
        [string] $Directory,
        [switch] $CurrentEnvironmentOnly
    ) 
    
    $sourceServExtractTo = "SourceServerExtractToDirectory"
    
    CreateDirectoryIfNeeded $Directory

    if ($CurrentEnvironmentOnly)
    {
        $env:_NT_SOURCE_PATH = "SRV*" + $Directory
    }
    else
    {
        if (Test-Path -Path $devTenDebuggerRegKey)
        {
            Set-ItemProperty -Path $devTenDebuggerRegKey -Name $sourceServExtractTo -Value $Directory 
        }
    
        if (Test-Path -Path $devElevenDebuggerRegKey)
        {
            Set-ItemProperty -Path $devElevenDebuggerRegKey -Name $sourceServExtractTo -Value $Directory 
        }
    
        if (Test-Path -Path $devTwelveDebuggerRegKey)
        {
            Set-ItemProperty -Path $devTwelveDebuggerRegKey -Name $sourceServExtractTo -Value $Directory 
        }

        if (Test-Path -Path $devFourteenDebuggerRegKey)
        {
            Set-ItemProperty -Path $devFourteenDebuggerRegKey -Name $sourceServExtractTo -Value $Directory 
        }

        # Always set the _NT_SOURCE_PATH value for WinDBG.
        Set-ItemProperty -Path HKCU:\Environment -Name _NT_SOURCE_PATH -Value "SRV*$Directory"
    }
        
}

###############################################################################

function Get-SymbolServer
{
<#
.SYNOPSIS
Returns a hashtable of the current symbol server settings.

.DESCRIPTION
Returns a hashtable with the current source server directories settings
for VS 2010, VS 2012, VS 2013, VS 2015, and the _NT_SYMBOL_PATH enviroment 
variable.

.LINK
http://www.wintellect.com/blogs/jrobbins
https://github.com/Wintellect/WintellectPowerShell
#>
    GetCommonSettings SymbolCacheDir _NT_SYMBOL_PATH
}

###############################################################################

function Get-SourceServerFiles
{
<#
.SYNOPSIS
Prepopulate your symbol cache with all your Source Server extracted source
code.

.DESCRIPTION
Recurses the specified symbol cache directory for PDB files with Source Server
sections and extracts the source code. This script is a simple wrapper around
SRCTOOl.EXE from the Debugging Tools for Windows (AKA WinDBG). If WinDBG is in 
the PATH this script will find SRCTOOL.EXE. If WinDBG is not in your path, use 
the SrcTool parameter to specify the complete path to the tool.

.PARAMETER CacheDirectory 
The required cache directory for the local machine.

.PARAMETER SrcTool
The optional parameter to specify where SRCTOOL.EXE resides.

.OUTPUTS 
HashTable
The keys are, if all present, VS 2010, VS 2012, VS 2013, VS 2015, and WinDBG. 
The values are those set for each debugger.

.LINK
http://www.wintellect.com/blogs/jrobbins
https://github.com/Wintellect/WintellectPowerShell
#>
    param ( 
        [Parameter(Mandatory=$true,
                   HelpMessage="Please specify the source server directory")]
        [string] $CacheDirectory ,
        [Parameter(HelpMessage="The optional full path to SCRTOOL.EXE")]
        [string] $SrcTool = ""
    ) 
    
    if ($SrcTool -eq "")
    {
        # Go with the default of looking up WinDBG in the path.
        $windbg = Get-Command -Name windbg.exe -ErrorAction SilentlyContinue
        if ($windbg -eq $null)
        {
            throw "Please use the -SrcTool parameter or have WinDBG in the path"
        }
        
        $windbgPath = Split-Path -Path ($windbg.Definition)
        $SrcTool = $windbgPath + "\SRCSRV\SRCTOOL.EXE"
    }
    
    if ((Get-Command -Name $SrcTool -ErrorAction SilentlyContinue) -eq $null)
    {
        throw "SRCTOOL.EXE does not exist."
    }
    
    if ((Test-Path -Path $CacheDirectory) -eq $false)
    {
        throw "The specified cache directory does not exist."
    }
    
    $cmd = $SrcTool
    
    # Get all the PDB files, execute SRCTOOL.EXE on each one.
    Get-ChildItem -Recurse -Include *.pdb -Path $cacheDirectory | `
        ForEach-Object { &$SrcTool -d:$CacheDirectory -x $_.FullName }

}

###############################################################################

function Set-SymbolServer
{
<#
.SYNOPSIS
Sets up a computer to use a symbol server.

DESCRIPTION
Sets up both the _NT_SYMBOL_PATH environment variable as well as VS 2010, VS 2012, 
VS 2013, and VS 2015 (if installed) to use a common symbol cache directory as well 
as common symbol servers. Optionally can be used to only set _NT_SYMBOL_PATH for 
an individual PowerShell window.

.PARAMETER Internal
Sets the symbol server to use to http://SymWeb. Visual Studio will not use the 
public symbol servers. This will turn off the .NET Framework Source Stepping. 
This switch is intended for internal Microsoft use only. You must specify either 
-Internal or -Public to the script.

.PARAMETER Public
Sets the symbol server to use as the two public symbol servers from Microsoft. 
All the appropriate settings are configured to properly have .NET Reference 
Source stepping working.

.PARAMETER CacheDirectory
Defaults to C:\SYMBOLS\PUBLIC for -Public and C:\SYMBOLS\INTERNAL for -Internal.

.PARAMETER SymbolServers
A string array of additional symbol servers to use. If -Internal is set, these 
additional symbol servers will appear before HTTP://SYMWEB. If -Public is set, 
these symbol servers will appear before the public symbol servers so both the 
environment variable and Visual Studio have the same search order.

.PARAMETER CurrentEnvironmentOnly
If specified will only set the current PowerShell window _NT_SYMBOL_PATH 
environment variable and not overwrite the global settings. This is primarily
for use with WinDBG as Visual Studio requires registry settings for the
cache directory.

.LINK
http://www.wintellect.com/blogs/jrobbins
https://github.com/Wintellect/WintellectPowerShell
#>
    [CmdLetBinding(SupportsShouldProcess=$true)]
    param ( [switch]   $Internal ,
    		[switch]   $Public ,
    		[string]   $CacheDirectory ,
    		[string[]] $SymbolServers = @(),
            [switch]   $CurrentEnvironmentOnly)
            
    # Do the parameter checking.
    if ( $Internal -eq $Public )
    {
        throw "You must specify either -Internal or -Public"
    }

    # Check if VS is running if we are going to be setting the global stuff. 
    if (($CurrentEnvironmentOnly -eq $false) -and (Get-Process -Name 'devenv' -ErrorAction SilentlyContinue))
    {
        throw "Visual Studio is running. Please close all instances before running this script"
    }
    
    if ($Internal)
    {
    	if ( $CacheDirectory.Length -eq 0 )
    	{
        	$CacheDirectory = "C:\SYMBOLS\INTERNAL" 
    	}
        
        $symPath = ""

        for ( $i = 0 ; $i -lt $SymbolServers.Length ; $i++ )
        {
            $symPath += "SRV*$CacheDirectory*"
            $symPath += $SymbolServers[$i]
            $symPath += ";"
    	}
        
        $symPath += "SRV*$CacheDirectory*http://SYMWEB"

        if ($CurrentEnvironmentOnly)
        {
            CreateDirectoryIfNeeded -directory $CacheDirectory
            $env:_NT_SYMBOL_PATH = $symPath
        }
        else
        {
            Set-ItemPropertyScript HKCU:\Environment _NT_SYMBOL_PATH $symPath

            if (Test-Path -Path $devTenDebuggerRegKey)
            {
        
                SetInternalSymbolServer $devTenDebuggerRegKey $CacheDirectory $symPath
            }

            if (Test-Path -Path $devElevenDebuggerRegKey)
            {
        
                SetInternalSymbolServer $devElevenDebuggerRegKey $CacheDirectory $symPath
            }

            if (Test-Path -Path $devTwelveDebuggerRegKey)
            {
        
                SetInternalSymbolServer $devTwelveDebuggerRegKey $CacheDirectory $symPath
            }

            if (Test-Path -Path $devFourteenDebuggerRegKey)
            {
        
                SetInternalSymbolServer $devFourteenDebuggerRegKey $CacheDirectory $symPath
            }
        }
    }
    else
    {
    
        if ( $CacheDirectory.Length -eq 0 )
    	{
        	$CacheDirectory = "C:\SYMBOLS\PUBLIC" 
    	}

        # It's public so we have a little different processing to do as there are
        # two public symbol servers where MSFT provides symbols.
        $refSrcPath = "$CacheDirectory*http://referencesource.microsoft.com/symbols"
        $msdlPath = "$CacheDirectory*http://msdl.microsoft.com/download/symbols"
        $extraPaths = ""
        
        # Poke on any additional symbol servers. I've keeping everything the
        # same between VS as WinDBG.
    	for ( $i = 0 ; $i -lt $SymbolServers.Length ; $i++ )
    	{
            $extraPaths += "SRV*$CacheDirectory*"
            $extraPaths += $SymbolServers[$i]
            $extraPaths += ";"
    	}

        $envPath = "$extraPaths" + "SRV*$refSrcPath;SRV*$msdlPath"
    
        if ($CurrentEnvironmentOnly)
        {
            CreateDirectoryIfNeeded -directory $CacheDirectory
            $env:_NT_SYMBOL_PATH = $envPath
        }
        else
        {
            Set-ItemPropertyScript HKCU:\Environment _NT_SYMBOL_PATH $envPath
    
            if (Test-Path -Path $devTenDebuggerRegKey)
            {
                SetPublicSymbolServer $devTenDebuggerRegKey $CacheDirectory
            }
        
            if (Test-Path -Path $devElevenDebuggerRegKey)
            {
                SetPublicSymbolServer $devElevenDebuggerRegKey $CacheDirectory
            }

            if (Test-Path -Path $devTwelveDebuggerRegKey)
            {
                SetPublicSymbolServer $devTwelveDebuggerRegKey $CacheDirectory
            }

            if (Test-Path -Path $devFourteenDebuggerRegKey)
            {
                SetPublicSymbolServer $devFourteenDebuggerRegKey $CacheDirectory
            }
        }
    }

    if ($CurrentEnvironmentOnly)
    {
        Write-Host -Object "`nThe _NT_SYMBOL_PATH environment variable was updated for this window only`n"
    }
    else
    {
        Write-Host -Object "`nPlease log out to activate the new symbol server settings`n"
    }
}

###############################################################################
# SIG # Begin signature block
# MIIYSwYJKoZIhvcNAQcCoIIYPDCCGDgCAQExCzAJBgUrDgMCGgUAMGkGCisGAQQB
# gjcCAQSgWzBZMDQGCisGAQQBgjcCAR4wJgIDAQAABBAfzDtgWUsITrck0sYpfvNR
# AgEAAgEAAgEAAgEAAgEAMCEwCQYFKw4DAhoFAAQUPNN8QM7KTqQrYw3qbdjfwML/
# U6SgghM8MIIEhDCCA2ygAwIBAgIQQhrylAmEGR9SCkvGJCanSzANBgkqhkiG9w0B
# AQUFADBvMQswCQYDVQQGEwJTRTEUMBIGA1UEChMLQWRkVHJ1c3QgQUIxJjAkBgNV
# BAsTHUFkZFRydXN0IEV4dGVybmFsIFRUUCBOZXR3b3JrMSIwIAYDVQQDExlBZGRU
# cnVzdCBFeHRlcm5hbCBDQSBSb290MB4XDTA1MDYwNzA4MDkxMFoXDTIwMDUzMDEw
# NDgzOFowgZUxCzAJBgNVBAYTAlVTMQswCQYDVQQIEwJVVDEXMBUGA1UEBxMOU2Fs
# dCBMYWtlIENpdHkxHjAcBgNVBAoTFVRoZSBVU0VSVFJVU1QgTmV0d29yazEhMB8G
# A1UECxMYaHR0cDovL3d3dy51c2VydHJ1c3QuY29tMR0wGwYDVQQDExRVVE4tVVNF
# UkZpcnN0LU9iamVjdDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAM6q
# gT+jo2F4qjEAVZURnicPHxzfOpuCaDDASmEd8S8O+r5596Uj71VRloTN2+O5bj4x
# 2AogZ8f02b+U60cEPgLOKqJdhwQJ9jCdGIqXsqoc/EHSoTbL+z2RuufZcDX65OeQ
# w5ujm9M89RKZd7G3CeBo5hy485RjiGpq/gt2yb70IuRnuasaXnfBhQfdDWy/7gbH
# d2pBnqcP1/vulBe3/IW+pKvEHDHd17bR5PDv3xaPslKT16HUiaEHLr/hARJCHhrh
# 2JU022R5KP+6LhHC5ehbkkj7RwvCbNqtMoNB86XlQXD9ZZBt+vpRxPm9lisZBCzT
# bafc8H9vg2XiaquHhnUCAwEAAaOB9DCB8TAfBgNVHSMEGDAWgBStvZh6NLQm9/rE
# JlTvA73gJMtUGjAdBgNVHQ4EFgQU2u1kdBScFDyr3ZmpvVsoTYs8ydgwDgYDVR0P
# AQH/BAQDAgEGMA8GA1UdEwEB/wQFMAMBAf8wEQYDVR0gBAowCDAGBgRVHSAAMEQG
# A1UdHwQ9MDswOaA3oDWGM2h0dHA6Ly9jcmwudXNlcnRydXN0LmNvbS9BZGRUcnVz
# dEV4dGVybmFsQ0FSb290LmNybDA1BggrBgEFBQcBAQQpMCcwJQYIKwYBBQUHMAGG
# GWh0dHA6Ly9vY3NwLnVzZXJ0cnVzdC5jb20wDQYJKoZIhvcNAQEFBQADggEBAE1C
# L6bBiusHgJBYRoz4GTlmKjxaLG3P1NmHVY15CxKIe0CP1cf4S41VFmOtt1fcOyu9
# 08FPHgOHS0Sb4+JARSbzJkkraoTxVHrUQtr802q7Zn7Knurpu9wHx8OSToM8gUmf
# ktUyCepJLqERcZo20sVOaLbLDhslFq9s3l122B9ysZMmhhfbGN6vRenf+5ivFBjt
# pF72iZRF8FUESt3/J90GSkD2tLzx5A+ZArv9XQ4uKMG+O18aP5cQhLwWPtijnGMd
# ZstcX9o+8w8KCTUi29vAPwD55g1dZ9H9oB4DK9lA977Mh2ZUgKajuPUZYtXSJrGY
# Ju6ay0SnRVqBlRUa9VEwggSTMIIDe6ADAgECAhBHio77WeHYPwzhQtKihwe+MA0G
# CSqGSIb3DQEBBQUAMIGVMQswCQYDVQQGEwJVUzELMAkGA1UECBMCVVQxFzAVBgNV
# BAcTDlNhbHQgTGFrZSBDaXR5MR4wHAYDVQQKExVUaGUgVVNFUlRSVVNUIE5ldHdv
# cmsxITAfBgNVBAsTGGh0dHA6Ly93d3cudXNlcnRydXN0LmNvbTEdMBsGA1UEAxMU
# VVROLVVTRVJGaXJzdC1PYmplY3QwHhcNMTAwNTEwMDAwMDAwWhcNMTUwNTEwMjM1
# OTU5WjB+MQswCQYDVQQGEwJHQjEbMBkGA1UECBMSR3JlYXRlciBNYW5jaGVzdGVy
# MRAwDgYDVQQHEwdTYWxmb3JkMRowGAYDVQQKExFDT01PRE8gQ0EgTGltaXRlZDEk
# MCIGA1UEAxMbQ09NT0RPIFRpbWUgU3RhbXBpbmcgU2lnbmVyMIIBIjANBgkqhkiG
# 9w0BAQEFAAOCAQ8AMIIBCgKCAQEAvDWgNnAigRHDsoO50yjGNs0la6l7shz2m1Gc
# 7zX07QiOXjgI+Hc8CkLg83Dco9fK9UwLz/8inAp+aNYJoiqEe6adtKnBM+LvHxdI
# yjrNRubFqne943ea+kdTQChZQ5PxpIHq74C1T6cIzrpuvMp2DJdkWYYkuz2CkKhV
# sZLToKcFrJ9TJQgQR5nNmN5o5bRQeKOvAcxZQ1jkdm5+rMfinh9PsEctyAyjSSeA
# dYy7BpFlD5Cb9LrRgchcauwU6SUJvyMW9JVGQEAhu4OW/YYfesgNEI6i+BkHWH+f
# vTcCYPKk6Z1EPzAF5KdwmVGa6BfxVcqyYYllRqdq8lhGfqqgBwIDAQABo4H0MIHx
# MB8GA1UdIwQYMBaAFNrtZHQUnBQ8q92Zqb1bKE2LPMnYMB0GA1UdDgQWBBQuLbAK
# RErTh8ACB86XfVBiIP0PgzAOBgNVHQ8BAf8EBAMCBsAwDAYDVR0TAQH/BAIwADAW
# BgNVHSUBAf8EDDAKBggrBgEFBQcDCDBCBgNVHR8EOzA5MDegNaAzhjFodHRwOi8v
# Y3JsLnVzZXJ0cnVzdC5jb20vVVROLVVTRVJGaXJzdC1PYmplY3QuY3JsMDUGCCsG
# AQUFBwEBBCkwJzAlBggrBgEFBQcwAYYZaHR0cDovL29jc3AudXNlcnRydXN0LmNv
# bTANBgkqhkiG9w0BAQUFAAOCAQEAyPtj+At1dSw68fITpy22oxqcrQEH0zSOd+DC
# bq4CXUhPpNIhtjb9KjVDfGvfgIcLFfB2MgC0zrVnpC8vIBucVJ6DPx9fFJVigg8i
# QSIfcLPz90LebFHNS/ghrJs7jLHl5iiPziqK+apSTYxbd7pNWljbu2oEzFIeneIo
# Nw675w6Rx/jb8YGY6803sw6rZdNi7DqldusTqDWTyS4KAezA6Mw9frbr4sHs0xSS
# gmaHUNz9UJess0p2cwbEhhE6s19DBFJv6rPQdDZMyvEbeYQ3cGOtdLmqDvOYsIYI
# 69vgH4wQ8jlkm65PCiySik8YtZHljRqTXx+u8abwLpfQ0vYrPDCCBOcwggPPoAMC
# AQICEBBwnU/1VAjXMGAB2OqRdbswDQYJKoZIhvcNAQEFBQAwgZUxCzAJBgNVBAYT
# AlVTMQswCQYDVQQIEwJVVDEXMBUGA1UEBxMOU2FsdCBMYWtlIENpdHkxHjAcBgNV
# BAoTFVRoZSBVU0VSVFJVU1QgTmV0d29yazEhMB8GA1UECxMYaHR0cDovL3d3dy51
# c2VydHJ1c3QuY29tMR0wGwYDVQQDExRVVE4tVVNFUkZpcnN0LU9iamVjdDAeFw0x
# MTA4MjQwMDAwMDBaFw0yMDA1MzAxMDQ4MzhaMHsxCzAJBgNVBAYTAkdCMRswGQYD
# VQQIExJHcmVhdGVyIE1hbmNoZXN0ZXIxEDAOBgNVBAcTB1NhbGZvcmQxGjAYBgNV
# BAoTEUNPTU9ETyBDQSBMaW1pdGVkMSEwHwYDVQQDExhDT01PRE8gQ29kZSBTaWdu
# aW5nIENBIDIwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDL+Oen6PGX
# KEBogDfSyD+OkoqSN4dHgOpMmc9u+RVHve519ESs0MPUTfcZwNM8TcFHuVllFpOM
# 2QqEm5/o9mpjWP5f3NF/S1GfABwAh1QgB1eggskvmK8zirt7gCIlamyvwixseRO9
# oypI1rWOYVXpa+g9gL8UA4UYjn5M6cIZiHOScs36/1BNyyymexpzsQCQLNky4vv9
# rJVCNuw0xRNTaLLBn0Cf2nvInWJsk6JC13mfl08xW1Ahoauv2RyyznW+WyxWACSN
# EcF1H/D+0pX+8OExIxhnwFsT/VqYlJT//1kCHwCs5vHy+jpzsx1C/FR1z1ExL+Pb
# gdl3IypPWc4jAgMBAAGjggFKMIIBRjAfBgNVHSMEGDAWgBTa7WR0FJwUPKvdmam9
# WyhNizzJ2DAdBgNVHQ4EFgQUHsWxLH2H2gJofCW8DAeEP7bP3vEwDgYDVR0PAQH/
# BAQDAgEGMBIGA1UdEwEB/wQIMAYBAf8CAQAwEwYDVR0lBAwwCgYIKwYBBQUHAwMw
# EQYDVR0gBAowCDAGBgRVHSAAMEIGA1UdHwQ7MDkwN6A1oDOGMWh0dHA6Ly9jcmwu
# dXNlcnRydXN0LmNvbS9VVE4tVVNFUkZpcnN0LU9iamVjdC5jcmwwdAYIKwYBBQUH
# AQEEaDBmMD0GCCsGAQUFBzAChjFodHRwOi8vY3J0LnVzZXJ0cnVzdC5jb20vVVRO
# QWRkVHJ1c3RPYmplY3RfQ0EuY3J0MCUGCCsGAQUFBzABhhlodHRwOi8vb2NzcC51
# c2VydHJ1c3QuY29tMA0GCSqGSIb3DQEBBQUAA4IBAQCViXeTaAFefNktNweQXVpC
# XgxktDa1D/ar1Tkn3iJGpEkcZktGGVkueUkD9pyS321QNVwMkS5gA1nQ8WT3aQn2
# fv7rNLNtsb9mnKO6MXi5hzVhPZIxG+/06J7WrEX6DDY8gGe7ve8uwpDhPXEvO8Gw
# WH5Fw1JxAwf28zlNizYhGwHf2dpeK+sOl4AeRBxQiPXGEjNKqE2ljS+UDHvGv5os
# wzLNvYwnJvDhMANQBoK89Duzg3UGxu+67tOA+FLGrMt58jiee7CSWEKRBciWIa25
# SxaBFGnxN7D+NPfcsN+X9UMQm3aPtGX16J8Ttx6sb8Rpil+6PGF+XkmGIxMurxVI
# MIIFLjCCBBagAwIBAgIQcX+oqSFbgNLgcUZ+Dxnw8jANBgkqhkiG9w0BAQUFADB7
# MQswCQYDVQQGEwJHQjEbMBkGA1UECBMSR3JlYXRlciBNYW5jaGVzdGVyMRAwDgYD
# VQQHEwdTYWxmb3JkMRowGAYDVQQKExFDT01PRE8gQ0EgTGltaXRlZDEhMB8GA1UE
# AxMYQ09NT0RPIENvZGUgU2lnbmluZyBDQSAyMB4XDTEzMTAyODAwMDAwMFoXDTE4
# MTAyODIzNTk1OVowgZ0xCzAJBgNVBAYTAlVTMQ4wDAYDVQQRDAUzNzkzMjELMAkG
# A1UECAwCVE4xEjAQBgNVBAcMCUtub3h2aWxsZTESMBAGA1UECQwJU3VpdGUgMzAy
# MR8wHQYDVQQJDBYxMDIwNyBUZWNobm9sb2d5IERyaXZlMRMwEQYDVQQKDApXaW50
# ZWxsZWN0MRMwEQYDVQQDDApXaW50ZWxsZWN0MIIBIjANBgkqhkiG9w0BAQEFAAOC
# AQ8AMIIBCgKCAQEAwVChJi7aiU+FAZeCy6rQcHAexAGgVu4Chh9fdZWnbDx2+OKS
# pL7jzoKZPHYQwf94puBqtU/ScYgDbroE1DkZfHOIYoaTlj6Dvh0Hbr2LwbYHbhdZ
# dlBzDQ1NyHqhRxe6raQ6RynWyuHg+n4dAH+pHfepBbMRbvIyWmgj5LA2hr2nVZBo
# 4/OgB8l2JKAidYaCuUDFXBwRPQCZVBApDaWAnLNCRcgCJHHIk9KAptmuFNrv8Eyb
# fdJaYb0rkaUVrDvocVXV7j2/yGtMMUknsIBDPrkhxrOOodNoo59iGw+GIWub8CmQ
# 9S4lvjkx4Q+azwPMPyVNJB+jt4uQbaE23GRGZwIDAQABo4IBiTCCAYUwHwYDVR0j
# BBgwFoAUHsWxLH2H2gJofCW8DAeEP7bP3vEwHQYDVR0OBBYEFASL4+TI2KlI7ozS
# jFNcSGhsQ9pbMA4GA1UdDwEB/wQEAwIHgDAMBgNVHRMBAf8EAjAAMBMGA1UdJQQM
# MAoGCCsGAQUFBwMDMBEGCWCGSAGG+EIBAQQEAwIEEDBGBgNVHSAEPzA9MDsGDCsG
# AQQBsjEBAgEDAjArMCkGCCsGAQUFBwIBFh1odHRwczovL3NlY3VyZS5jb21vZG8u
# bmV0L0NQUzBBBgNVHR8EOjA4MDagNKAyhjBodHRwOi8vY3JsLmNvbW9kb2NhLmNv
# bS9DT01PRE9Db2RlU2lnbmluZ0NBMi5jcmwwcgYIKwYBBQUHAQEEZjBkMDwGCCsG
# AQUFBzAChjBodHRwOi8vY3J0LmNvbW9kb2NhLmNvbS9DT01PRE9Db2RlU2lnbmlu
# Z0NBMi5jcnQwJAYIKwYBBQUHMAGGGGh0dHA6Ly9vY3NwLmNvbW9kb2NhLmNvbTAN
# BgkqhkiG9w0BAQUFAAOCAQEAHibwVe5iTcPaZVhne++CGpFJFWASomYbtgEG/Z5A
# KT0Jgwvfu5uliKAfckPNYwgNLyx+/qHMnNji2BG5jb2skEzJpZHDbbwgQ4uQtmpK
# L8k7E5Pg07Ithpw5IPUMcfrdgWeUMWm35lEA4ps9q8bua5b3sVlPd5sK8sIlisuV
# hLKbgTwf/LsgJKKNtogG3/Me0VfxEm9XtuKO/FkjXGqorLH2HIX/iA1Yyr25CITA
# gbXdVP9SY0JzcpwKzL1+qdat4WHvoS4j1quPVVE4bYVphB1rEPY772eX67EWY4x4
# 4eqFBaW/nt4712js8jo+JUYxOSILZ4VN4EQdbKUJQUOQMjGCBHkwggR1AgEBMIGP
# MHsxCzAJBgNVBAYTAkdCMRswGQYDVQQIExJHcmVhdGVyIE1hbmNoZXN0ZXIxEDAO
# BgNVBAcTB1NhbGZvcmQxGjAYBgNVBAoTEUNPTU9ETyBDQSBMaW1pdGVkMSEwHwYD
# VQQDExhDT01PRE8gQ29kZSBTaWduaW5nIENBIDICEHF/qKkhW4DS4HFGfg8Z8PIw
# CQYFKw4DAhoFAKB4MBgGCisGAQQBgjcCAQwxCjAIoAKAAKECgAAwGQYJKoZIhvcN
# AQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEOMAwGCisGAQQBgjcCARUw
# IwYJKoZIhvcNAQkEMRYEFHEGnuXTCk4XGUE04vPTGO+kZ+t0MA0GCSqGSIb3DQEB
# AQUABIIBAJWRrypm2A3NsFDNGRxwrbARyrirjEft0raYDd0ZUgw9xcgTuNl9Uahn
# OfE3jQXDcgij/4CYC5s9Ya44tnzQCmDFF3RRh4qhuYbEut1+uHAOanpUEwaJo2HS
# NZYv4tXQuRTTcty6+Z1f/YF2JH0/ybmDMruGhGt1bzU0X1BQSW5nFVDwBq/AR4gi
# exm+qVVwfhFuEFYCuTklGM83/bkC61yvgDYE6FvY107K/ZpSnWyrT2xWVwLZPLoQ
# cCoZ9agusEWBGmKsY1IB9Kh5YFqfXoMl2ryF3UtLg4z4gviGZrMuScrAcG1aNj0W
# xzZbX+PnpD95wXo2WD1TAx7KGhD/52ihggJEMIICQAYJKoZIhvcNAQkGMYICMTCC
# Ai0CAQAwgaowgZUxCzAJBgNVBAYTAlVTMQswCQYDVQQIEwJVVDEXMBUGA1UEBxMO
# U2FsdCBMYWtlIENpdHkxHjAcBgNVBAoTFVRoZSBVU0VSVFJVU1QgTmV0d29yazEh
# MB8GA1UECxMYaHR0cDovL3d3dy51c2VydHJ1c3QuY29tMR0wGwYDVQQDExRVVE4t
# VVNFUkZpcnN0LU9iamVjdAIQR4qO+1nh2D8M4ULSoocHvjAJBgUrDgMCGgUAoF0w
# GAYJKoZIhvcNAQkDMQsGCSqGSIb3DQEHATAcBgkqhkiG9w0BCQUxDxcNMTQxMTEy
# MTgwMzI1WjAjBgkqhkiG9w0BCQQxFgQU+rlb87XFOchB4Pr2vkKsMoaMdEMwDQYJ
# KoZIhvcNAQEBBQAEggEAb3JDn0uCSJdETL8oagtqIDa8V9XtubXAAIpSv4Q7j4zl
# WM+8/NyLbl1Wnt2SF3vyviT2S6ck6Vw/Y7QJcA3GQ59ezw5GkpP49TTcx/NG3+5w
# Y9DKbEcgzY/m1OwFbPb7m+nAiQP2hSy2SJt9N9cY1ZdkAWdugLPdgQm7HB4qcVtS
# SdEE2jGAW4WS5ghWCPEsBQaAsOO3fo4n2f7gYPN1fXvfBEiI2QQGo8Yi4cgT0ZH6
# VTVQnjeaf7lODN11SZbXG5TgtO2PuFYXAhjAQ1BdkcIHne6sqdGUPiCggnc+EqRh
# PZsXvNgyk4CLmhluiks1R5CMStAzhlveOuva+lfTrQ==
# SIG # End signature block
