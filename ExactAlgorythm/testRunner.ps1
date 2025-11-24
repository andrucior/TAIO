# testRunner.ps1

param(
    [string]$TestDirectory = "testy",
    [string]$OutputLog = "log.txt"
)

# Wyczyszczenie poprzedniego logu
"=== Test run $(Get-Date) ===" | Out-File $OutputLog

# Pobranie wszystkich plików txt
$files = Get-ChildItem $TestDirectory -Filter *.txt

foreach ($file in $files) {
    Add-Content $OutputLog "`nRunning test: $($file.Name)"

    # Uruchomienie dotnet run i przekierowanie stdout + stderr
    $result = dotnet run -- $file.FullName 2>&1

    Add-Content $OutputLog $result
    Add-Content $OutputLog "-------------------------------"
}

Write-Host "Finished. Results saved to $OutputLog"
