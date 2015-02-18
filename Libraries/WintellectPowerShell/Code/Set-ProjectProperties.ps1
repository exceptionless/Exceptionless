#requires -version 2.0
###############################################################################
# WintellectPowerShell Module
# Copyright (c) 2010-2014 - John Robbins/Wintellect
# 
# Do whatever you want with this module, but please do give credit.
###############################################################################

# Always make sure all variables are defined and all best practices are 
# followed.
Set-StrictMode  –version Latest 

###############################################################################
# Script Global Variables
###############################################################################
# The namespace for everything in a VS project file.
$script:BuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003"

# These are the settings used as the defaults for the general property group.
$script:DefaultDotNetGeneralProperties = @{
}

# The default properties for both debug and release builds.
$script:DefaultDotNetConfigProperties = @{
# Stop the build on any compilation warnings.
"TreatWarningsAsErrors" = "true";
# Always check for numeric wrapping. Does not cause perf issues.
"CheckForOverflowUnderflow" = "true";
# Always run code analysis. This will set the Microsoft minimum rules if the CodeAnalysisRuleSet
# property is not specified.
"RunCodeAnalysis" = "true";
# Always produce an XML document file. This script block gets the binary output directory and
# puts the doc comment file there as well.
"DocumentationFile" = 
        {
            # The configuration to add the DocumentationFile element into.
            param($config)

            # Go find the main property group to get the name of the assembly.
            $assemblyName = $config.ParentNode.GetElementsByTagName("AssemblyName")[0].InnerText

            # Set the output name to be the path. This works in both C# and VB.
            $valueName = Join-Path -Path $config.OutputPath -ChildPath "$assemblyName.XML"

            ReplaceNode -document $config.ParentNode.ParentNode `
                        -topElement $config `
                        -elementName "DocumentationFile" `
                        -elementValue $valueName
        }
}

# The array of default rulesets when setting the <CodeAnalysisRulesSet>
# property so I don't try to do relative paths when setting it.
$script:BuiltInRulesets = 
"AllRules.ruleset",
"BasicCorrectnessRules.ruleset",
"BasicDesignGuidelineRules.ruleset",
"ExtendedCorrectnessRules.ruleset",
"ExtendedDesignGuidelineRules.ruleset",               
"GlobalizationRules.ruleset",
"ManagedMinimumRules.ruleset",
"MinimumRecommendedRules.ruleset",
"MixedMinimumRules.ruleset",
"MixedRecommendedRules.ruleset",
"NativeMinimumRules.ruleset",
"NativeRecommendedRules.ruleset",
"SecurityRules.ruleset"                              

###############################################################################
# Public Cmdlets
###############################################################################
function Set-ProjectProperties([string[]]  $paths,
                               [switch]    $OverrideDefaultProperties,
                               [string[]]  $Configurations = @("Debug", "Release"),
                               [HashTable] $CustomGeneralProperties = @{},
                               [HashTable] $CustomConfigurationProperties = @{})
{
<#
.SYNOPSIS
A script to make Visual Studio 2010 and higher project management easier.

.DESCRIPTION
When you need to make a simple change to a number of Visual Studio projects, 
it can be a large pain to manually go through and do those changes, especially 
since it's so easy to forget a project or mess up. This script's job is to 
automate the process so it's repeatable and consistent.

If you do not specify any custom options, the script will automatically update
projects with the following settings. 

[Note that at this time only C# projects are supported.]

C# Debug and Release configurations
---------------
-	Treat warnings as errors
-	Check for arithmetic overflow and underflow
-	Enable code analysis with the default Code Analysis settings file.
-	Turn on creation of XML doc comment files.

This script is flexible and you can control down to setting/changing an 
individual property if necessary. There are many examples in the Examples 
section.

.PARAMETER  paths
This script can take pipeline input so you can easily handle deeply nested
project trees. Alternatively, you can put wildcards in this, but recursive
directories will not be searched.

.PARAMETER OverrideDefaultProperties
If set, will not apply the default settings built into the script and only
take the properties to change with the CustomGeneralProperties and 
CustomConfigurationProperties parameters.

.PARAMETER Configurations
The array of configurations you want to change in the project file these are
matching strings so if you specify something like 'Debug|AnyCPU' you are 
narrowing down the configuration to search. The default is 'Debug' and 
'Release'.

.PARAMETER CustomGeneralProperties
The hash table for the general properties such as TargetFrameworkVersion, 
FileAlignment and other properties on the Application or Signing tab when
looking at the project properties. The key is the property name and the 
value is either the string, or a script block that will be called to do 
custom processing. The script block will be passed the XML for all the 
global project properties so it can do additional processing.

.PARAMETER CustomConfigurationProperties
The hash table for the properties such as TreatWarningsAsErrors and 
RunCodeAnalysis which are per build configuration(s). Like the 
CustomGeneralProperties, the hash table key is the property to set and the
value is the string to set or a script block for advanced processing. The 
script block will be passed the current configuration. See the examples
for how this can be used.

.EXAMPLE
dir -recurse *.csproj | Set-ProjectProperties

Recursively updates all the C# project files in the current directory with 
all the default settings.

.EXAMPLE

dir A.csproj | `
    Set-ProjectProperties `
        -CustomGeneralProperties @{"AssemblyOriginatorKeyFile" = "c:\dev\ConsoleApplication1.snk"} 

Updates A.CSPROJ to the default settings and adds the strong name key to the 
general properties. When specifying the AssemblyOriginatorKeyFile this script 
will treat file correctly and make it a relative path from the .CSPROJ folder 
location. When specifying a file, use the full path to the file so everything 
works correctly.

.EXAMPLE

dir B.csproj | `
    Set-ProjectProperties `
        -CustomConfigurationProperties @{ "CodeAnalysisRuleSet" = "c:\dev\WintellectRuleSet.ruleset"}

Updates B.CSPROJ to the default settings and sets all configurations to 
enable Code Analysis with the custom rules file specified. Always specify the 
full path to the custom ruleset file as the script will handle making all 
references to it relative references in the configurations.

If you specify one of the default Code Analysis rules files that shipped with 
Visual Studio, the script properly handles those as well. You can find all the 
default ruleset files by looking in the 
"<VS Install Dir>\Team Tools\Static Analysis Tools\Rule Sets" folder.

.EXAMPLE

dir C.csproj | Set-ProjectProperties `
        -OverrideDefaultProperties `
        -Configurations "Release" `
        -CustomConfigurationProperties @{ "DefineConstants" = 
                {
                    param($config)
                    $defines = $config.GetElementsByTagName("DefineConstants")
                    $defines[0].InnerText = $defines[0].InnerText + ";FOOBAR"
                } 
            }

Updates C.CSPROJ by only adding a new define to only the Release configuration, 
keeping any existing define and not using the default changes.

.INPUTS
The Visual Studio project files to change.

.NOTES
Obviously, to maximize your usage you should be familiar with all the 
properties in Visual Studio project files and the properties in them. 
See http://msdn.microsoft.com/en-us/library/0k6kkbsd.aspx for more information.

.LINK
http://www.wintellect.com/blogs/jrobbins
http://code.wintellect.com
#>
    begin
    {
        function ReplaceNode(        $document,
                                     $topElement,
                            [string] $elementName,
                            [string] $elementValue )
        {
            Write-Debug -Message "Replacing $elementName=$elementValue"

            $origNode = $topElement[$elementName]
            if ($origNode -eq $null)
            {
                $node = $document.CreateElement($elementName,$script:BuildNamespace)
                $node.InnerText = $elementValue

                [void]$topElement.AppendChild($node)
            }
            else
            {
                $origNode.InnerText = $elementValue
            }
        }

        function ReplaceRelativePathNode([string] $fileLocation,
                                                  $document,
                                                  $topElement,
                                        [string]  $elementName,
                                        [String]  $fullUseFilePath)
        {
            try
            {
                Push-Location -Path (Split-Path -Path $fileLocation)

                $relLocation = Resolve-Path -Path $fullUseFilePath -Relative

                Write-Debug -Message "Setting relative path $elementName=$relLocation"

                ReplaceNode -document $document `
                            -topElement $topElement `
                            -elementName $elementName `
                            -elementValue $relLocation
            }
            finally
            {
                Pop-Location
            }
        }

        function HandleCSharpMainProperties([string]    $file, 
                                            [xml]       $fileXML, 
                                            [hashtable] $newMainProps )
        {
            # Go find the main property group which is the one with the ProjectGuid in it.
            $mainProps = $fileXML.Project.PropertyGroup | Where-Object { $_["ProjectGuid"] -ne $null }

            if (($mainProps -eq $null) -or ($mainProps -is [Array]))
            {
                throw "$file does not have the correct property group with the ProjectGuid or has multiple"
            }

            # Enumerate through the property keys.
            foreach ($item in $newMainProps.GetEnumerator())
            {
                switch ($item.Key)
                {
                    "AssemblyOriginatorKeyFile" 
                    {
                        # Get the full path to the .SNK file specified.
                        $snkFile = Resolve-Path -Path $item.Value -ErrorAction SilentlyContinue

                        if ($snkFile -eq $null)
                        {
                            [string]$inputFile = $item.Value
                            throw "Unable to find $inputFile, Please specify the full path to the file."
                        }

                        ReplaceRelativePathNode -fileLocation $file `
                                                -document $fileXML `
                                                -topElement $mainProps `
                                                -elementName "AssemblyOriginatorKeyFile" `
                                                -fullUseFilePath $snkFile

                        # In case the user forgot, set the option to use the SNK file also.
                        ReplaceNode -document $fileXML `
                                    -topElement $mainProps `
                                    -elementName "SignAssembly" `
                                    -elementValue "true"
                    }

                    default
                    {
                        ReplaceNode -document $fileXML `
                                    -topElement $mainProps `
                                    -elementName $item.Key `
                                    -elementValue $item.Value
                    }
                }
            }
        }

        function HandleCSharpConfigProperties([string]    $file,
                                              [xml]       $allFileXML,
                                              [string]    $configString,
                                              [HashTable] $newProps)
        {
            # Get the configuration propery group.
            $configGroup = $allFileXML.GetElementsByTagName("PropertyGroup") | Where-Object { ($_.GetAttribute("Condition") -ne "") -and ($_.Condition -match $configString) }

            if (($configGroup -eq $null) -or ($configGroup -is [Array]))
            {
                throw "$file does not have the $configString property group or has multiple."
            }


            foreach($item in $newProps.GetEnumerator())
            {
                # Have to treat the CodeAnalysisRuleSet property special so we get the 
                # relative path set.
                if ($item.Key -eq "CodeAnalysisRuleSet")
                {
                    # Is the ruleset file one of the default files?
                    if ($script:BuiltInRulesets -contains $item.Value)
                    {
                        # Simple enough, plop in the default name and go on.
                        ReplaceNode -document $allFileXML `
                                    -topElement $configGroup `
                                    -elementName $item.Key `
                                    -elementValue $item.Value
                    }
                    else
                    {
                        # Get the full path to the .RuleSet file specified.
                        $ruleFile = Resolve-Path -Path $item.Value -ErrorAction SilentlyContinue

                        if ($ruleFile -eq $null)
                        {
                            [string]$inputFile = $item.Value
                            throw "Unable to find $inputFile, Please specify the full path to the file."
                        }

                        ReplaceRelativePathNode -fileLocation $file `
                                                -document $allFileXML `
                                                -topElement $configGroup `
                                                -elementName $item.Key `
                                                -fullUseFilePath $ruleFile

                    }

                    # In case the user forgot, set the option to turn on using the code analysis file.
                    ReplaceNode -document $fileXML `
                                -topElement $configGroup `
                                -elementName "RunCodeAnalysis" `
                                -elementValue "true"

                }
                elseif ($item.Value -is [scriptblock])
                {
                    & $item.Value $configGroup
                }
                else
                {
                    ReplaceNode -document $allFileXML `
                                -topElement $configGroup `
                                -elementName $item.Key `
                                -elementValue $item.Value
                }
            }
        }

        function ProcessCSharpProjectFile([string] $file)
        {

            # Try and read the file as XML. Let the errors go if it's not.
            [xml]$fileXML = Get-Content -Path $file


            # Build up the property hash values.
            [HashTable]$mainPropertiesHash = @{}
            [HashTable]$configPropertiesHash = @{}

            # Does the user just want to apply their properties?
            if ($OverrideDefaultProperties)
            {
                $mainPropertiesHash = $CustomGeneralProperties
                $configPropertiesHash = $CustomConfigurationProperties
            }
            else
            {
                $mainPropertiesHash = $script:DefaultDotNetGeneralProperties.Clone()
                if ($CustomGeneralProperties.Count -gt 0)
                {
                    $mainPropertiesHash = Merge-HashTables -htold $mainPropertiesHash -htnew $CustomGeneralProperties
                }

                $configPropertiesHash = $script:DefaultDotNetConfigProperties.Clone()
                if ($CustomConfigurationProperties.Count -gt 0)
                {
                    $configPropertiesHash = Merge-HashTables -htold $configPropertiesHash -htnew $CustomConfigurationProperties
                }
            }

            # Are there any main properties to change?
            if ($mainPropertiesHash.Count -gt 0)
            {
               HandleCSharpMainProperties -file $file -fileXML $fileXML -newMainProps $mainPropertiesHash
            }

            # Any configuration properties to change?
            if ($configPropertiesHash.Count -gt 0)
            {
                # Loop through the configuration array.
                foreach($config in $Configurations)
                {
                    HandleCSharpConfigProperties -file $file -allFileXML $fileXML -configString $config -newProps $configPropertiesHash
                }
            }

            $fileXML.Save($file)
        }

        function ProcessProjectFile([string] $file)
        {
            # Is the file read only?
            if ((Get-ChildItem -Path $file).IsReadOnly)
            {
                throw "$file is readonly so it cannot be changed"
            }

            $ext = [System.IO.Path]::GetExtension($file)

            switch -Wildcard ($ext)
            {
                "*.csproj"
                {
                    ProcessCSharpProjectFile -file $file
                }

                default
                {
                    throw "Sorry, $file is an unsupported project type at this time."
                }
            }


        }
    }
    process
    {
        if ($_)
        {
            ProcessProjectFile $_
        }
    }
    end
    {
        if ($paths)
        {
            # Loop through each item on the command line.
            foreach ($path in $paths)
            {
                # There might be a wildcard here so resolve it to an array.
                $resolvedPaths = Resolve-Path -Path $path
                foreach ($file in $resolvedPaths)
                {
                    ProcessProjectFile $file
                }
            }
        }
    }
}

# SIG # Begin signature block
# MIIYSwYJKoZIhvcNAQcCoIIYPDCCGDgCAQExCzAJBgUrDgMCGgUAMGkGCisGAQQB
# gjcCAQSgWzBZMDQGCisGAQQBgjcCAR4wJgIDAQAABBAfzDtgWUsITrck0sYpfvNR
# AgEAAgEAAgEAAgEAAgEAMCEwCQYFKw4DAhoFAAQUaFgEW4hhdahXYCF1otpVnoQ3
# 4l6gghM8MIIEhDCCA2ygAwIBAgIQQhrylAmEGR9SCkvGJCanSzANBgkqhkiG9w0B
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
# IwYJKoZIhvcNAQkEMRYEFDl6fiFrB3mePMhjKgcOlOgYQfNGMA0GCSqGSIb3DQEB
# AQUABIIBAI+W9AUdefLX7i/3IlqIDa7dxYod/jDs8d8Sf9ekpA1iLa+IT5EYLo/i
# TVFUmocEGBLSjBosgImxFPo8mSBrJxh6AjikDhRKf+pw7jZvSQgmfl1Fv64FITXX
# wulmSp3oRRvablPje0sRAycvmaZhf2pzWsDOFMMnEk0UNk43/2ob2BeJVh+Oxcf8
# /WyrH9rX+qfbYnVgeBFH8Lesqu+p4qf+Xx/aMiUPPJgxTqIarMmIu54Q9vNR1sdG
# Xwm4pxseLdLvnNS26riotjnjrPjeEZWctgpt20+6s/IN3NPOaMR8FXFHzzFccbg8
# eghp4ki14PMnj1FRCHfCTnXbrwdYg+ChggJEMIICQAYJKoZIhvcNAQkGMYICMTCC
# Ai0CAQAwgaowgZUxCzAJBgNVBAYTAlVTMQswCQYDVQQIEwJVVDEXMBUGA1UEBxMO
# U2FsdCBMYWtlIENpdHkxHjAcBgNVBAoTFVRoZSBVU0VSVFJVU1QgTmV0d29yazEh
# MB8GA1UECxMYaHR0cDovL3d3dy51c2VydHJ1c3QuY29tMR0wGwYDVQQDExRVVE4t
# VVNFUkZpcnN0LU9iamVjdAIQR4qO+1nh2D8M4ULSoocHvjAJBgUrDgMCGgUAoF0w
# GAYJKoZIhvcNAQkDMQsGCSqGSIb3DQEHATAcBgkqhkiG9w0BCQUxDxcNMTQxMTEy
# MTgwMzI0WjAjBgkqhkiG9w0BCQQxFgQUwWF+7LRrAEhiK48KHGC9WbnshPEwDQYJ
# KoZIhvcNAQEBBQAEggEAilm5ScYVDZCD4zUpFdRfCVV097zSBQaAatpV83CMuL7G
# 9hHgaLHYQA0vspESpN4oCQ91uRZd9HD7jizy4oc/2wxZ+TZXkAxy9Mj4PZxn8kfl
# ADEwUbq96jDIU8Hz3rGA63rU5TWyVD0a+GapS/1qCDu7fbI09EmaOVqviZCFN8gW
# H6uyum/pIfEyO5OoLuZdOA/kf8uWuezsR4rj3LRdfiqknWW1ZymJpUSrqdx0Tho0
# pU9cQ95QNc4Ud7gTIB+mrzvOfUM5Stckz9fTf1N17npkHd7hQB5FbNDDiIUHUFp9
# gJC0HHMH39DiHPN+YDFOsP7zp0jTW29pfvRkhbFmlA==
# SIG # End signature block
