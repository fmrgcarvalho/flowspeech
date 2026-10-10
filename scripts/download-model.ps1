<#
.SYNOPSIS
  Descarrega um modelo Whisper (formato GGML) para %AppData%\FlowPT\models.

.EXAMPLE
  ./scripts/download-model.ps1                 # large-v3-turbo quantizado (recomendado, ~550 MB)
  ./scripts/download-model.ps1 -Model small    # mais leve (~190 MB), para PCs sem GPU fracos
#>
param(
    [ValidateSet("turbo", "small", "medium", "large")]
    [string]$Model = "turbo"
)

$files = @{
    turbo  = "ggml-large-v3-turbo-q5_0.bin"
    small  = "ggml-small-q5_1.bin"
    medium = "ggml-medium-q5_0.bin"
    large  = "ggml-large-v3-q5_0.bin"
}

$file = $files[$Model]
$dir  = Join-Path $env:APPDATA "FlowPT\models"
$dest = Join-Path $dir $file
$url  = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$file"

New-Item -ItemType Directory -Force -Path $dir | Out-Null

if (Test-Path $dest) {
    Write-Host "Já existe: $dest"
} else {
    Write-Host "A descarregar $url ..."
    $ProgressPreference = "SilentlyContinue"   # acelera muito o Invoke-WebRequest
    Invoke-WebRequest -Uri $url -OutFile $dest
    Write-Host "Guardado em $dest"
}

if ($Model -ne "turbo") {
    Write-Host ""
    Write-Host "Atualiza Transcription.ModelPath em %AppData%\FlowPT\settings.json para: $file"
}
