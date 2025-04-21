param([string]$version)
echo $version
$versionNumbers = $version.Split(".")

# Safe fallback version
$oldVersion = "12.0.0"  # Or any existing older stable version of AutoMapper
echo "Using fallback old version: $oldVersion"
