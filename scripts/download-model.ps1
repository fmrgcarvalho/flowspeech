<#
.SYNOPSIS
  Descarrega o modelo de transcrição para %AppData%\FlowSpeech\models.

.EXAMPLE
  ./scripts/download-model.ps1                   # Parakeet TDT 0.6B v3 int8 (por omissão, ~0,7 GB)
  ./scripts/download-model.ps1 -Engine whisper   # Whisper large-v3-turbo q5_0 (alternativa, ~550 MB)
#>
param(
    [ValidateSet("parakeet", "whisper")]
    [string]$Engine = "parakeet"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"   # acelera muito o Invoke-WebRequest

$dir = Join-Path $env:APPDATA "FlowSpeech\models"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

if ($Engine -eq "parakeet") {
    $name    = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8"
    $target  = Join-Path $dir $name
    $archive = Join-Path $dir "$name.tar.bz2"
    $url     = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/$name.tar.bz2"

    if (Test-Path (Join-Path $target "encoder.int8.onnx")) {
        Write-Host "Já existe: $target"
        return
    }

    Write-Host "A descarregar $url ..."
    Invoke-WebRequest -Uri $url -OutFile $archive

    Write-Host "A extrair ..."
    tar -xjf $archive -C $dir          # tar.exe vem incluído no Windows 10/11
    Remove-Item $archive

    $testWavs = Join-Path $target "test_wavs"
    if (Test-Path $testWavs) { Remove-Item $testWavs -Recurse -Force }

    Write-Host "Modelo pronto em $target"
}
else {
    $file = "ggml-large-v3-turbo-q5_0.bin"
    $dest = Join-Path $dir $file
    $url  = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$file"

    if (Test-Path $dest) {
        Write-Host "Já existe: $dest"
    } else {
        Write-Host "A descarregar $url ..."
        Invoke-WebRequest -Uri $url -OutFile $dest
        Write-Host "Guardado em $dest"
    }

    Write-Host ""
    Write-Host 'Para usar o Whisper, em %AppData%\FlowSpeech\settings.json define "Engine": "whisper".'
}
