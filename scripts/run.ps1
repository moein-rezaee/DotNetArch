Param(
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$ArgsPassThru
)

$sdkVersion = (& dotnet --version)
if (-not $sdkVersion) { throw "dotnet SDK not found" }
$major = [int]($sdkVersion.Split('.')[0])
$tfm = if ($major -ge 9) { 'net9.0' } else { 'net8.0' }

dotnet run -f $tfm -- $ArgsPassThru

