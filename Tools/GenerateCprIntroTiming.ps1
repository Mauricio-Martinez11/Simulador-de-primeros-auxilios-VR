param([string]$Project = (Split-Path $PSScriptRoot -Parent), [string]$Key="")
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Project $Project -Key $Key; if($LASTEXITCODE -ne 0) { throw "Error al generar audio sincronizado" }; return }
# Native Sabina output rate keeps SpeakProgress.AudioPosition aligned with WAV time.
Add-Type -AssemblyName System.Speech
Add-Type -ReferencedAssemblies System.Speech -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
public class RcpWord { public float time; public int start; public int length; }
public class RcpSpeechTiming {
 public List<RcpWord> words=new List<RcpWord>();
 public void Record(object sender, SpeakProgressEventArgs e) { words.Add(new RcpWord {time=(float)e.AudioPosition.TotalSeconds,start=e.CharacterPosition,length=e.CharacterCount}); }
 public void Bind(SpeechSynthesizer s) { s.SpeakProgress+=Record; }
}
"@
$speech=New-Object System.Speech.Synthesis.SpeechSynthesizer
$speech.SelectVoice('Microsoft Sabina Desktop'); $speech.Rate=-1
$timing=New-Object RcpSpeechTiming; $timing.Bind($speech)
$format=New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000,[System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen,[System.Speech.AudioFormat.AudioChannel]::Mono)
$folder=Join-Path $Project 'Assets/Resources/RCP/Narration'
$data=Get-Content -LiteralPath (Join-Path $folder 'Script.json') -Raw -Encoding UTF8 | ConvertFrom-Json
try {
 foreach($entry in $data.entries) {
  if($Key -and $entry.key -ne $Key) { continue }
  if($entry.key -notin @('MenuWelcome','Look','Hands','SelectHands','SelectControllers','ControlsDone')) { continue }
  $timing.words.Clear()
  $speech.SetOutputToWaveFile((Join-Path $folder ($entry.key+'.wav')),$format)
  $spoken=$entry.text.Replace('mixta','míksta')
  $speech.Speak($spoken); $speech.SetOutputToNull()
  # Map phonetic spelling back to the unchanged on-screen Spanish dialogue.
  $changes=[regex]::Matches($spoken,'míksta')
  foreach($word in $timing.words) {
   $offset=0
   foreach($change in $changes) {
    if($word.start -gt $change.Index) { $offset++ }
    elseif($word.start -eq $change.Index) { $word.length=5 }
   }
   $word.start-=$offset
  }
  @{words=@($timing.words.ToArray())} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $folder ($entry.key+'Timing.json')) -Encoding UTF8
  $copy=Join-Path $Project ('Assets/Audio/RCP/'+$entry.key+'.wav')
  if(Test-Path -LiteralPath $copy) { Copy-Item -LiteralPath (Join-Path $folder ($entry.key+'.wav')) -Destination $copy }
  Write-Output ($entry.key+': '+$timing.words.Count+' palabras sincronizadas')
 }
} finally { $speech.Dispose() }

