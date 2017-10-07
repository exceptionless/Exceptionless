Push-Location $PSScriptRoot

.\Start-ElasticSearch.ps1 -StartKibana -SkipNodeStart -OpenKibana

Pop-Location