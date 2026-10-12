# Verify the actual single-file app and its published exe through PowerShell/native pipes.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$executionRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executionSource = Join-Path $executionRepo 'samples/Tansu.Forms.SingleFile/ActionTool.cs'
$executionArtifacts = Join-Path $executionRepo 'artifacts/cli-verification'
$executionRun = Join-Path $executionArtifacts ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $executionRun -Force | Out-Null
$executionStore = Join-Path $executionRun 'production.json'
$executionResults = [Collections.Generic.List[string]]::new()
function Assert-Execution([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Invoke-ExecutionTool([string[]]$ToolArguments, [string]$RequestInput, [switch]$Published) {
    $executionErrorFile = Join-Path $executionRun ([guid]::NewGuid().ToString('N') + '.stderr.txt')
    $executionArguments = @($ToolArguments) + @('--param', "store=$executionStore")
    if ($Published) {
        if ($PSBoundParameters.ContainsKey('RequestInput')) {
            $executionOutput = $RequestInput | & $executionExecutable @executionArguments 2> $executionErrorFile
        } else {
            $executionOutput = & $executionExecutable @executionArguments 2> $executionErrorFile
        }
    } else {
        if ($PSBoundParameters.ContainsKey('RequestInput')) {
            $executionOutput = $RequestInput | dotnet run --file $executionSource --verbosity quiet -- @executionArguments 2> $executionErrorFile
        } else {
            $executionOutput = dotnet run --file $executionSource --verbosity quiet -- @executionArguments 2> $executionErrorFile
        }
    }
    $executionCode = $LASTEXITCODE
    [pscustomobject]@{ Code = $executionCode; Output = ($executionOutput -join "`n"); Error = (Get-Content -LiteralPath $executionErrorFile -Raw) }
}
try {
    $executionBin = Join-Path $executionRun 'bin'
    dotnet publish $executionSource --verbosity quiet --output $executionBin
    Assert-Execution ($LASTEXITCODE -eq 0) 'Publishing the single-file app failed.'
    $executionExecutable = Join-Path $executionBin 'ActionTool.exe'
    Assert-Execution (Test-Path -LiteralPath $executionExecutable) 'The published exe was not generated.'

    $executionSelect = Invoke-ExecutionTool -ToolArguments @('SELECT')
    Assert-Execution ($executionSelect.Code -eq 0 -and -not $executionSelect.Error -and @($executionSelect.Output | ConvertFrom-Json).Count -eq 2) 'File-based SELECT did not produce a clean JSON array.'
    $executionSearch = Invoke-ExecutionTool -ToolArguments @('SELECT', '--param', 'PartNumber=p-', '--param', 'minGoodCount=100', '--param', 'maxGoodCount=120') -Published
    $executionMatches = @($executionSearch.Output | ConvertFrom-Json)
    Assert-Execution ($executionSearch.Code -eq 0 -and -not $executionSearch.Error -and $executionMatches.Count -eq 1 -and $executionMatches[0].partNumber -eq 'P-102') 'SELECT did not combine case-insensitive name and inclusive quantity range conditions.'
    $executionSearch = Invoke-ExecutionTool -ToolArguments @('select', '--param', 'partNumber=P-205', '--param', 'minGoodCount=100') -Published
    Assert-Execution ($executionSearch.Code -eq 0 -and $executionSearch.Output.Trim() -eq '[]') 'SELECT must apply all conditions and return an empty array when none match.'
    $executionSearch = Invoke-ExecutionTool -ToolArguments @('SELECT', '--id', '1', '--param', 'partNumber=P-205') -Published
    Assert-Execution ($executionSearch.Code -eq 3 -and -not $executionSearch.Output) 'SELECT id ignored the additional search condition.'
    foreach ($executionConditions in @(@('minGoodCount=bad'), @('minGoodCount=-1'), @('minGoodCount=121', 'maxGoodCount=120'))) {
        $executionSearchArgs = @('SELECT')
        foreach ($executionCondition in $executionConditions) { $executionSearchArgs += @('--param', $executionCondition) }
        $executionSearch = Invoke-ExecutionTool -ToolArguments $executionSearchArgs -Published
        Assert-Execution ($executionSearch.Code -eq 2 -and -not $executionSearch.Output) 'Invalid SELECT range conditions must produce an input failure.'
    }
    $executionResults.Add('PASS: SELECT combines name/id/range conditions, accepts case-insensitive names and rejects invalid ranges.')
    $executionInsert = Invoke-ExecutionTool -ToolArguments @('INSERT', '--body', '-') -RequestInput '{"partNumber":"日本語パイプ","goodCount":23}'
    $executionRecord = $executionInsert.Output | ConvertFrom-Json
    Assert-Execution ($executionInsert.Code -eq 0 -and -not $executionInsert.Error -and $executionRecord.id -gt 0 -and $executionRecord.partNumber -eq '日本語パイプ') 'File-based INSERT did not consume stdin and return the saved record.'
    $executionSelect = Invoke-ExecutionTool -ToolArguments @('SELECT', '--id', $executionRecord.id.ToString()) -Published
    Assert-Execution ($executionSelect.Code -eq 0 -and ($executionSelect.Output | ConvertFrom-Json).goodCount -eq 23) 'The exe did not see data saved by the file-based process.'
    $executionRecord.goodCount = 200
    $executionUpdate = Invoke-ExecutionTool -ToolArguments @('UPDATE', '--id', $executionRecord.id.ToString(), '--body', '-') -RequestInput ($executionRecord | ConvertTo-Json -Compress) -Published
    Assert-Execution ($executionUpdate.Code -eq 0 -and -not $executionUpdate.Error -and ($executionUpdate.Output | ConvertFrom-Json).goodCount -eq 200) 'PowerShell object transformation did not reach UPDATE.'
    $executionResults.Add('PASS: dotnet run/exe SELECT, stdin INSERT, cross-process persistence, Unicode and PowerShell object transformation into UPDATE.')

    $executionLeftError = Join-Path $executionRun 'pipe-left.stderr.txt'
    $executionRightError = Join-Path $executionRun 'pipe-right.stderr.txt'
    $executionPipe = & $executionExecutable SELECT --id $executionRecord.id --param "store=$executionStore" 2> $executionLeftError |
        & $executionExecutable UPDATE --id $executionRecord.id --body - --param "store=$executionStore" 2> $executionRightError
    Assert-Execution ($LASTEXITCODE -eq 0 -and ($executionPipe | ConvertFrom-Json).goodCount -eq 200) 'An exe-to-exe pipe failed.'
    Assert-Execution (-not (Get-Content -LiteralPath $executionLeftError -Raw) -and -not (Get-Content -LiteralPath $executionRightError -Raw)) 'Native pipe stderr was not empty.'
    $executionPipe = dotnet run --file $executionSource --verbosity quiet -- SELECT --id $executionRecord.id --param "store=$executionStore" 2> $executionLeftError |
        dotnet run --file $executionSource --verbosity quiet -- UPDATE --id $executionRecord.id --body - --param "store=$executionStore" 2> $executionRightError
    Assert-Execution ($LASTEXITCODE -eq 0 -and ($executionPipe | ConvertFrom-Json).goodCount -eq 200) 'A file-based app to file-based app pipe failed.'
    $executionResults.Add('PASS: exe-to-exe and dotnet-run-to-dotnet-run pipes transfer the returned record as the next body.')

    $executionBad = Invoke-ExecutionTool -ToolArguments @('INSERT', '--body', '-') -RequestInput '{"partNumber":"","goodCount":-1}' -Published
    Assert-Execution ($executionBad.Code -eq 2 -and -not $executionBad.Output -and ($executionBad.Error | ConvertFrom-Json).fieldErrors.PartNumber.Count -gt 0) 'Validation errors did not use stderr/exit 2.'
    $executionBad = Invoke-ExecutionTool -ToolArguments @('INSERT', '--body', '-') -RequestInput '' -Published
    Assert-Execution ($executionBad.Code -eq 2 -and -not $executionBad.Output) 'Empty upstream output did not stop registration.'
    $executionBad = Invoke-ExecutionTool -ToolArguments @('PATCH') -Published
    Assert-Execution ($executionBad.Code -eq 2 -and -not $executionBad.Output) 'Removed HTTP methods must be rejected as invalid operations.'
    $executionBad = Invoke-ExecutionTool -ToolArguments @('SELECT', '--id', '999999') -Published
    Assert-Execution ($executionBad.Code -eq 3 -and -not $executionBad.Output) 'Missing targets did not use exit 3.'
    $executionDelete = Invoke-ExecutionTool -ToolArguments @('DELETE', '--id', $executionRecord.id.ToString()) -Published
    Assert-Execution ($executionDelete.Code -eq 0 -and ($executionDelete.Output | ConvertFrom-Json).deleted) 'DELETE did not return its result.'
    $executionResults.Add('PASS: Invalid input, empty stdin, unsupported methods, missing records and DELETE follow the JSON/error/exit contract.')
    $executionResults.Add('All single-file CLI checks passed.')
} catch {
    $executionResults.Add($_.ToString())
    throw
} finally {
    $executionResults | Set-Content -LiteralPath (Join-Path $executionArtifacts 'results.txt') -Encoding utf8
    $executionResults | Write-Output
}
