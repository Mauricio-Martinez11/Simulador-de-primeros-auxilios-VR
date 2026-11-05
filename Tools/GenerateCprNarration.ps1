param([string]$Project = (Split-Path $PSScriptRoot -Parent), [string]$Key = "")
$ErrorActionPreference = 'Stop'
$timedKeys=@('MenuWelcome','Look','Hands','SelectHands','SelectControllers','ControlsDone')
if (-not $Key -or $Key -in $timedKeys) { & (Join-Path $PSScriptRoot 'GenerateCprIntroTiming.ps1') -Project $Project -Key $Key }
Add-Type -AssemblyName System.Speech
$speech = New-Object System.Speech.Synthesis.SpeechSynthesizer
$speech.SelectVoice('Microsoft Sabina Desktop')
$speech.Rate = -1
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050,[System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen,[System.Speech.AudioFormat.AudioChannel]::Mono)
$folder = Join-Path $Project 'Assets/Resources/RCP/Narration'
$scriptData = Get-Content -LiteralPath (Join-Path $folder 'Script.json') -Raw -Encoding UTF8 | ConvertFrom-Json
try {
    foreach ($entry in $scriptData.entries) {
        if ($entry.key -in $timedKeys) { continue }
        if ($Key -and $entry.key -ne $Key) { continue }
        $speech.SetOutputToWaveFile((Join-Path $folder ($entry.key + '.wav')), $format)
        $speech.Speak($entry.text.Replace("mixta","míksta"))
        $speech.SetOutputToNull()
        $copy = Join-Path $Project ('Assets/Audio/RCP/'+$entry.key+'.wav')
        if(Test-Path -LiteralPath $copy) { Copy-Item -LiteralPath (Join-Path $folder ($entry.key+'.wav')) -Destination $copy }
        Write-Output ('Audio generado: ' + $entry.key)
    }
} finally { $speech.Dispose() }
