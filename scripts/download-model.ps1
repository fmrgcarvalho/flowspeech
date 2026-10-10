<#
.SYNOPSIS
  Descarrega o modelo de transcrição para %AppData%\FlowSpeech\models.

.EXAMPLE
  ./scripts/download-model.ps1                   # Parakeet TDT 0.6B v3 int8 (por omissão, ~0,7 GB)
  ./scripts/download-model.ps1 -Engine whisper   # Whisper large-v3-turbo q5_0 (alternativa, ~550 MB)
  ./scripts/download-model.ps1 -Engine ai        # Qwen 2.5 1.5B Instruct Q4_K_M para limpeza com IA (~1 GB)
#>
param(
    [ValidateSet("parakeet", "whisper", "ai")]
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
elseif ($Engine -eq "ai") {
    $file = "qwen2.5-1.5b-instruct-q4_k_m.gguf"
    $dest = Join-Path $dir $file
    $url  = "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/$file"

    if (Test-Path $dest) {
        Write-Host "Já existe: $dest"
    } else {
        Write-Host "A descarregar $url ..."
        Invoke-WebRequest -Uri $url -OutFile $dest
        Write-Host "Guardado em $dest"
    }

    Write-Host ""
    Write-Host 'Liga a opção "Limpar texto com IA" no menu do ícone do FlowSpeech.'
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
