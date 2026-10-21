$ErrorActionPreference = 'Stop'
$pp = New-Object -ComObject PowerPoint.Application
$outDir = Join-Path (Get-Location) 'render'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$pres = $pp.Presentations.Open((Join-Path (Get-Location) 'Driftline-Portfolio.pptx'), $true, $false, $false)
foreach ($slide in $pres.Slides) {
  $slide.Export((Join-Path $outDir ('slide-{0:d2}.png' -f $slide.SlideIndex)), 'PNG', 1600, 900)
}
$pres.Close()
$pp.Quit()
Write-Output 'done'
