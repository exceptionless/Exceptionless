# Copyright 2014 Exceptionless
#
# This program is free software: you can redistribute it and/or modify it 
# under the terms of the GNU Affero General Public License as published 
# by the Free Software Foundation, either version 3 of the License, or 
# (at your option) any later version.
# 
#     http://www.gnu.org/licenses/agpl-3.0.html

Framework "4.5.1"

properties {
    $version =  "2.0"
    $configuration = "Release"

    $base_dir = Resolve-Path "."
    $source_dir = "$base_dir\Source"
    $lib_dir = "$base_dir\Libraries"
    $build_dir = "$base_dir\Build"
    $working_dir = "$build_dir\Working"
    $deploy_dir = "$build_dir\Deploy"
    $packages_dir = "$base_dir\Packages"

    $sln_file = "$base_dir\Exceptionless.ServerOnly.sln"
    $sign_file = "$source_dir\Exceptionless.snk"

    $client_projects = @(
        @{ Name = "Exceptionless"; 					SourceDir = "$source_dir\Clients\Shared";	ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Models.dll;"; },
        @{ Name = "Exceptionless.Signed"; 			SourceDir = "$source_dir\Clients\Shared";	ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Models.dll;"; },
        @{ Name = "Exceptionless.Mvc";  			SourceDir = "$source_dir\Clients\Mvc"; 		ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.Mvc.Signed";  		SourceDir = "$source_dir\Clients\Mvc"; 		ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.Nancy";  			SourceDir = "$source_dir\Clients\Nancy"; 	ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.WebApi";  			SourceDir = "$source_dir\Clients\WebApi"; 	ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.WebApi.Signed";  	SourceDir = "$source_dir\Clients\WebApi"; 	ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.Web"; 				SourceDir = "$source_dir\Clients\Web"; 		ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.Web.Signed"; 		SourceDir = "$source_dir\Clients\Web"; 		ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.Windows"; 			SourceDir = "$source_dir\Clients\Windows"; 	ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.Windows.Signed"; 	SourceDir = "$source_dir\Clients\Windows"; 	ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.Console"; 			SourceDir = "$source_dir\Clients\Console"; 	ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.Console.Signed"; 	SourceDir = "$source_dir\Clients\Console"; 	ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.Wpf"; 				SourceDir = "$source_dir\Clients\Wpf"; 		ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.Wpf.Signed"; 		SourceDir = "$source_dir\Clients\Wpf"; 		ExternalNuGetDependencies = $null;	MergeDependencies = "Exceptionless.Extras.dll;"; },
        @{ Name = "Exceptionless.NLog"; 			SourceDir = "$source_dir\Clients\NLog";		ExternalNuGetDependencies = "NLog";	MergeDependencies = $null; }
        @{ Name = "Exceptionless.NLog.Signed"; 		SourceDir = "$source_dir\Clients\NLog";		ExternalNuGetDependencies = "NLog";	MergeDependencies = $null; }
    )

    $client_build_configurations = @(
        @{ Constants = "EMBEDDED;PORTABLE40";	TargetFrameworkVersionProperty="NET40";	NuGetDir = "portable-net40+sl50+win+wpa81+wp80"; }
        @{ Constants = "EMBEDDED;PORTABLE40"; 	TargetFrameworkVersionProperty="NET40";	NuGetDir = "net40"; },
        @{ Constants = "EMBEDDED;PORTABLE40"; 	TargetFrameworkVersionProperty="NET45";	NuGetDir = "net45"; }
    )

    $client_test_projects = @(
        @{ Name = "Client.Tests";	BuildDir = "$source_dir\Clients\Tests\bin\$configuration"; }
    )

    $server_test_projects = @(
        @{ Name = "Exceptionless.Api.Tests";	BuildDir = "$source_dir\Api.Tests\bin\$configuration"; }
    )
}

Include .\teamcity.ps1

task default -depends Build, Test, Package
task client -depends PackageClient
task server -depends PackageServer

task Clean {
    Delete-Directory $build_dir
    Delete-Directory "$source_dir\JobRunner\bin"
    Delete-Directory "$source_dir\Api.IIS\App_Data"
}

task Init -depends Clean {
    Verify-BuildRequirements

    If (![string]::IsNullOrWhiteSpace($env:BUILD_NUMBER)) {
        $build_number = $env:BUILD_NUMBER
    } else {
        $build_number = "0"
    }

    If (![string]::IsNullOrWhiteSpace($env:BUILD_SUFFIX)) {
        $build_suffix = $env:BUILD_SUFFIX
    } else {
        $build_suffix = ""
    }
    
    If (![string]::IsNullOrWhiteSpace($env:BUILD_VCS_NUMBER_Exceptionless_Master)) {
        $git_hash = $env:BUILD_VCS_NUMBER_Exceptionless_Master.Substring(0, 10)
        TeamCity-ReportBuildProgress "VCS Revision: $git_hash"
    }

    $info_version = "$version.$build_number $git_hash".Trim()
    $script:nuget_version = "$version.$build_number$build_suffix"
    $version = "$version.$build_number"

    TeamCity-SetBuildNumber $version
    #$env:BUILD_NUMBER = $version
    
    Update-GlobalAssemblyInfo "$source_dir\GlobalAssemblyInfo.cs" $version $version $info_version	
}

task BuildClient -depends Init {
    ForEach ($p in $client_projects) {
        ForEach ($b in $client_build_configurations) {
            $isPclClient = ($($p.Name) -eq "Exceptionless") -or ($($p.Name) -eq "Exceptionless.Signed")
            If (($isPclClient -and ($($b.NuGetDir) -ne "portable-net40+sl50+win+wpa81+wp80")) -or (!$isPclClient -and ($($b.NuGetDir) -eq "portable-net40+sl50+win+wpa81+wp80"))) {
                Continue;
            }

            $outputDirectory = "$build_dir\$configuration\$($p.Name)\lib\$($b.NuGetDir)"

            TeamCity-ReportBuildStart "Building $($p.Name) ($($b.TargetFrameworkVersionProperty))" 

            If ($($p.Name).EndsWith(".Signed")) {
                $name = $($p.Name).Replace(".Signed", "");
                exec { & msbuild "$($p.SourceDir)\$name.csproj" `
                            /p:SignAssembly=true `
                            /p:AssemblyOriginatorKeyFile="$sign_file" `
                            /p:Configuration="$configuration" `
                            /p:Platform="AnyCPU" `
                            /p:DefineConstants="`"TRACE;SIGNED;$($b.Constants)`"" `
                            /p:OutputPath="$outputDirectory" `
                            /p:TargetFrameworkVersionProperty="$($b.TargetFrameworkVersionProperty)" `
                            /t:"Rebuild" }
            } else {
                exec { & msbuild "$($p.SourceDir)\$($p.Name).csproj" `
                            /p:SignAssembly=false `
                            /p:Configuration="$configuration" `
                            /p:Platform="AnyCPU" `
                            /p:DefineConstants="`"TRACE;$($b.Constants)`"" `
                            /p:OutputPath="$outputDirectory" `
                            /p:TargetFrameworkVersionProperty="$($b.TargetFrameworkVersionProperty)" `
                            /t:"Rebuild" }
            }

            TeamCity-ReportBuildFinish "Finished building $($p.Name) ($($b.TargetFrameworkVersionProperty))"
        }
    }

    TeamCity-ReportBuildStart "Building Client Tests" 
    exec { & msbuild "$source_dir\Clients\Tests\Client.Tests.csproj" `
        /p:Configuration="$configuration" `
        /t:"Rebuild" }
    TeamCity-ReportBuildFinish "Finished building Client Tests"
}

task BuildServer -depends Init {			
    TeamCity-ReportBuildStart "Building Server" 
    exec { msbuild "$sln_file" /p:Configuration="$configuration" /p:Platform="Any CPU" /t:Rebuild }
    TeamCity-ReportBuildFinish "Finished building Server"
}

task Build -depends BuildClient, BuildServer

task TestClient -depends BuildClient {
    TeamCity-ReportBuildProgress "Running Client Tests"
    ForEach ($p in $client_test_projects) {
        If (!(Test-Path -Path "$($p.BuildDir)\$($p.Name).dll")) {
            TeamCity-ReportBuildProblem "Unit test project $($p.Name) needs to be compiled first."
            Exit
        }

        exec { & "$lib_dir\xunit\xunit.console.clr4.exe" "$($p.BuildDir)\$($p.Name).dll"; }
    }
}

task TestServer -depends BuildServer {
    TeamCity-ReportBuildProgress "Running Server Tests"
    ForEach ($p in $server_test_projects) {
        If (!(Test-Path -Path "$($p.BuildDir)\$($p.Name).dll")) {
            TeamCity-ReportBuildProblem "Unit test project $($p.Name) needs to be compiled first."
            Exit
        }

        exec { & "$lib_dir\xunit\xunit.console.clr4.exe" "$($p.BuildDir)\$($p.Name).dll"; }
    }
}

task Test -depends TestClient, TestServer

task PackageClient -depends TestClient {
    Create-Directory $deploy_dir

    ForEach ($p in $client_projects) {
        $isSignedProject = $($p.Name).EndsWith(".Signed")
        $assemblyName = $($p.Name).Replace(".Signed", "")
        $workingDirectory = "$working_dir\$($p.Name)"
        Create-Directory $workingDirectory

        TeamCity-ReportBuildProgress "Building Client NuGet Package: $($p.Name)"

        #copy assemblies from build directory to working directory.
        ForEach ($b in $client_build_configurations) {
            $isPclClient = ($($p.Name) -eq "Exceptionless") -or ($($p.Name) -eq "Exceptionless.Signed")
            If (($isPclClient -and ($($b.NuGetDir) -ne "portable-net40+sl50+win+wpa81+wp80")) -or (!$isPclClient -and ($($b.NuGetDir) -eq "portable-net40+sl50+win+wpa81+wp80"))) {
                Continue;
            }

            $buildDirectory = "$build_dir\$configuration\$($p.Name)\lib\$($b.NuGetDir)"
            $workingLibDirectory = "$workingDirectory\lib\$($b.NuGetDir)"
            Create-Directory $workingLibDirectory

            #If ($($p.MergeDependencies) -ne $null) {
            #	ILMerge-Assemblies $buildDirectory $workingLibDirectory "$assemblyName.dll" "$($p.MergeDependencies)" "$($b.TargetFrameworkVersionProperty)"
            #} else {
            #	Copy-Item -Path "$buildDirectory\$assemblyName.dll" -Destination $workingLibDirectory
            #}

            # Work around until we are able to merge dependencies and update other project dependencies pre build (E.G., MVC client references Models)
            Get-ChildItem -Path $buildDirectory | Where-Object { $_.Name -eq "$assemblyName.dll" -Or $_.Name -eq "$assemblyName.pdb" -or $_.Name -eq "$assemblyName.xml" } | Copy-Item -Destination $workingLibDirectory

            If ($($p.MergeDependencies) -ne $null) {
                ForEach ($assembly in $($p.MergeDependencies).Split(";", [StringSplitOptions]"RemoveEmptyEntries")) {
                    Get-ChildItem -Path $buildDirectory | Where-Object { $_.Name -eq "$assembly" -Or $_.Name -eq "$assembly".Replace(".dll", ".pdb") -or $_.Name -eq "$assembly".Replace(".dll", ".xml") } | Copy-Item -Destination $workingLibDirectory
                }
            }
        }

        # Copy the source code for Symbol Source.
        robocopy $($p.SourceDir) $workingDirectory\src\$($p.SourceDir.Replace($base_dir, """")) *.cs *.xaml /S /NP
        robocopy "$base_dir\Source\Core" "$workingDirectory\src\Source\Core" *.cs /S /NP /XD obj
        robocopy "$base_dir\Source\Models" "$workingDirectory\src\Source\Models" *.cs /S /NP /XD obj
        Copy-Item "$base_dir\Source\GlobalAssemblyInfo.cs" "$workingDirectory\src\Source\GlobalAssemblyInfo.cs"

        If (($($p.Name) -ne "Exceptionless") -and ($($p.Name) -ne "Exceptionless.Signed")) {
            robocopy "$base_dir\Source\Clients\Extras" "$workingDirectory\src\Source\Clients\Extras" *.cs /S /NP /XD obj
        }

        If ($($p.Name).StartsWith("Exceptionless.Mvc")) {
            robocopy "$base_dir\Source\Clients\Web" "$workingDirectory\src\Source\Clients\Web" *.cs /S /NP /XD obj
        }

        If ((Test-Path -Path "$($p.SourceDir)\NuGet")) {
            Copy-Item "$($p.SourceDir)\NuGet\*" $workingDirectory -Recurse
        }

        Copy-Item "$($source_dir)\Clients\LICENSE.txt" "$workingDirectory"
        Copy-Item "$($source_dir)\Clients\Shared\NuGet\tools\exceptionless.psm1" "$workingDirectory\tools"

        $nuspecFile = "$workingDirectory\$($p.Name).nuspec"
        If ($isSignedProject){
            Rename-Item -Path "$workingDirectory\$assemblyName.nuspec" -NewName $nuspecFile
        }

        # update NuGet nuspec file.
        $nuspec = [xml](Get-Content $nuspecFile)
        If (($($p.ExternalNuGetDependencies) -ne $null) -and (Test-Path -Path "$($p.SourceDir)\packages.config")) {
            $packages = [xml](Get-Content "$($p.SourceDir)\packages.config")
            
            ForEach ($d in $($p.ExternalNuGetDependencies).Split(";", [StringSplitOptions]"RemoveEmptyEntries")) {
                $package = $packages.SelectSinglenode("/packages/package[@id=""$d""]")
                $nuspec | Select-Xml -XPath '//dependency' |% {
                    If ($_.Node.Id.Equals($d)){
                        $_.Node.Version = "$($package.version)"
                    }
                }
            }
        }

        If ($isSignedProject) {
            $nuspec | Select-Xml -XPath '//id' |% { $_.Node.InnerText = $_.Node.InnerText + ".Signed" }
            $nuspec | Select-Xml -XPath '//dependency' |% {
                If($_.Node.Id.StartsWith("Exceptionless")){
                    $_.Node.Id = $_.Node.Id + ".Signed"
                }
            }
        }

        $nuspec.Save($nuspecFile);

        $packageDir = "$deploy_dir\ClientPackages"
        Create-Directory $packageDir

        exec { & $base_dir\nuget\NuGet.exe pack $nuspecFile -OutputDirectory $packageDir -Version $nuget_version -Symbols }
    }

    Delete-Directory "$build_dir\$configuration"
    Delete-Directory $working_dir
}

task PackageServer -depends TestServer {
    Create-Directory $deploy_dir

    $packageDir = "$deploy_dir\ServerPackages"
    Create-Directory $packageDir
    
    robocopy "$source_dir\JobRunner\bin\$configuration" "$source_dir\JobRunner\bin\$configuration\bin" /XF Job.* NLog.config /MOVE /XD jobs Mail bin /E /NP

    robocopy "$source_dir\JobRunner\bin\$configuration" "$source_dir\Api.IIS\App_Data\JobRunner" /S /NP

    robocopy "$source_dir\Api.IIS\App_Data\JobRunner\jobs" "$source_dir\Api.IIS\App_Data\jobs" /MOVE /E /NP

    robocopy "$base_dir" "$source_dir\Api.IIS" DownloadGeoIPDatabase.ps1

    TeamCity-ReportBuildProgress "Building Server NuGet Package: Exceptionless.Api"
    exec { & $base_dir\nuget\NuGet.exe pack "$source_dir\Api.IIS\Exceptionless.Api.nuspec" -OutputDirectory $packageDir -Version $nuget_version -NoPackageAnalysis }
}

task Package -depends PackageClient, PackageServer

Function Update-GlobalAssemblyInfo ([string] $filename, [string] $assemblyVersionNumber, [string] $assemblyFileVersionNumber, [string] $assemblyInformationalVersionNumber) {
    $assemblyVersion = "AssemblyVersion(`"$assemblyVersionNumber`")"
    $assemblyFileVersion = "AssemblyFileVersion(`"$assemblyFileVersionNumber`")"
    $assemblyInformationalVersion = "AssemblyInformationalVersion(`"$assemblyInformationalVersionNumber`")"

    TeamCity-ReportBuildProgress "Version: $assemblyVersionNumber Bind Version: $assemblyFileVersionNumber Info Version: $assemblyInformationalVersionNumber"

    (Get-Content $filename) | ForEach-Object {
        % {$_ -replace 'AssemblyVersion\("[^"]+"\)', $assemblyVersion } |
        % {$_ -replace 'AssemblyVersion = "[^"]+"', "AssemblyVersion = `"$assemblyVersionNumber`"" } |
        % {$_ -replace 'AssemblyFileVersion\("[^"]+"\)', $assemblyFileVersion } |
        % {$_ -replace 'AssemblyFileVersion = "[^"]+"', "AssemblyFileVersion = `"$assemblyFileVersionNumber`"" } |
        % {$_ -replace 'AssemblyInformationalVersion\("[^"]+"\)', $assemblyInformationalVersion } |
        % {$_ -replace 'AssemblyInformationalVersion = "[^"]+"', "AssemblyInformationalVersion = `"$assemblyInformationalVersionNumber`"" }
    } | Set-Content $filename
}

Function Verify-BuildRequirements() {
    If ((ls "$env:windir\Microsoft.NET\Framework\v4.0*") -eq $null) {
        throw "Building Exceptionless requires .NET 4.0/4.5, which doesn't appear to be installed on this machine."
    }
}

Function ILMerge-Assemblies ([string] $sourceDir, [string] $destinationDir, [string] $sourceAssembly, [string] $assembliesToMerge, [string] $targetFramworkVersion) {
    Create-Directory $destinationDir

    $targetplatform = $null
    If (($targetFramworkVersion -eq "v4.5") -or ($targetFramworkVersion -eq "v4.0")) {
        $v4_net_version = (Resolve-Path -Path "$env:windir\Microsoft.NET\Framework\v4.0*")
        $targetplatform = "/targetplatform:`"v4,$v4_net_version`""
    }

    $assemblies = ""
    ForEach ($assembly in $assembliesToMerge.Split(";", [StringSplitOptions]"RemoveEmptyEntries")) {
        If (Test-Path -Path "$sourceDir\$assembly") {
            $assemblies += "$sourceDir\$assembly "
        } else {
            $assemblies += "$assembly "
        }
    }

    exec { & (Resolve-Path -Path "$((Get-PackagePath ilmerge))\ILMerge.exe") "$sourceDir\$sourceAssembly" `
        $assemblies `
        /out:"$destinationDir\$sourceAssembly" `
        /keyfile:"$sign_file" `
        /t:library `
        $targetplatform }
}

Function Create-Directory([string] $directory_name) {
    If (!(Test-Path -Path $directory_name)) {
        New-Item $directory_name -ItemType Directory | Out-Null
    }
}

Function Delete-Directory([string] $directory_name) {
    "Removing Directory: $directory_name)"
    Remove-Item -Force -Recurse $directory_name -ErrorAction SilentlyContinue
}

Function Get-PackagePath ([string] $packageName) {
    $packagePath = Get-ChildItem "$packages_dir\$packageName.*" |
                        Sort-Object Name -Descending | 
                        Select-Object -First 1

    Return "$packagePath"
}