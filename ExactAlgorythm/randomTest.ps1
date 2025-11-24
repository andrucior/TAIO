function New-RandomGraphTestFiles {
    param(
        [int]$Count = 5,
        [int]$n1 = 4,
        [int]$n2 = 10,
        [int]$k = 2,
        [string]$OutputDir = "./testy"
    )

    function New-Matrix($n) {
        $m = @()
        for ($i = 0; $i -lt $n; $i++) {
            $row = @()
            for ($j = 0; $j -lt $n; $j++) {
                if ($i -eq $j) { $row += 0 }
                else { $row += (Get-Random -Minimum 0 -Maximum 2) }
            }
            $m += ,$row
        }
        return $m
    }

    # Upewniamy się, że folder istnieje
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir | Out-Null
    }

    # Szukamy najwyższego istniejącego numeru testX*.txt
    $existing = Get-ChildItem $OutputDir -Filter "test*.txt" -ErrorAction Ignore
    $max = 0

    foreach ($file in $existing) {
        if ($file.BaseName -match "^test(\d+)") {
            $num = [int]$Matches[1]
            if ($num -gt $max) { $max = $num }
        }
    }

    # Nowe numery startują od max+1
    $nextNum = $max + 1

    for ($t = 1; $t -le $Count; $t++) {

        $g1 = New-Matrix $n1
        $g2 = New-Matrix $n2

        $lines = @()
        $lines += $n1
        foreach ($row in $g1) { $lines += ($row -join " ") }
        $lines += $n2
        foreach ($row in $g2) { $lines += ($row -join " ") }
        $lines += $k

        $file = Join-Path $OutputDir ("test{0}.txt" -f $nextNum)
        $nextNum++

        $lines | Out-File $file -Encoding utf8

        Write-Host "Generated file: $file"
    }
}
