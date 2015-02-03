Function Create-Directory([string] $directory_name) {
    If (!(Test-Path -Path $directory_name)) {
        New-Item $directory_name -ItemType Directory | Out-Null
    }
}

Function Expand-GZip ([string]$Source, [string]$OutputPath = ($Source -Replace "\.gz$", "")) {
	If (!(Test-Path -Path $Source -PathType Leaf)) {
        Write-Error -Message "$Source does not exist"
        Return
    }
	
	If (Test-Path -Path $OutputPath) {
		Remove-Item -Path $OutputPath
    }

    $input = New-Object System.IO.FileStream $Source, ([IO.FileMode]::Open), ([IO.FileAccess]::Read), ([IO.FileShare]::Read);
    $output = New-Object System.IO.FileStream $OutputPath, ([IO.FileMode]::CreateNew), ([IO.FileAccess]::Write), ([IO.FileShare]::None)
    $gzipStream = New-Object System.IO.Compression.GzipStream $input, ([IO.Compression.CompressionMode]::Decompress)
    
	Try {
        $buffer = New-Object byte[](1024);
        While ($true) {
            $read = $gzipStream.Read($buffer, 0, 1024)
            If ($read -le 0) {
                Break;
            }
            $output.Write($buffer, 0, $read)
        }
    } Finally {
        $gzipStream.Close();
        $output.Close();
        $input.Close();
    }
}

$base_dir = Resolve-Path "."
$MaxMindDBPath = "$base_dir\Source\Api.IIS\App_Data"

if (!(Test-Path -Path "$base_dir\Source\Api.IIS")) {
	$MaxMindDBPath = "$base_dir\App_Data"
}

Create-Directory $MaxMindDBPath
Start-BitsTransfer -Source "http://geolite.maxmind.com/download/geoip/database/GeoLite2-City.mmdb.gz" -Destination $MaxMindDBPath
Expand-GZip "$MaxMindDBPath\GeoLite2-City.mmdb.gz"
Remove-Item -Path "$MaxMindDBPath\GeoLite2-City.mmdb.gz"