$base = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
. (Join-Path $base "..\Shared\BuildBundle.Common.ps1")

Build-AbrBundle -Base $base -BundleName "AbrPlanStrip" -AssemblyName "Abr.Civil.PlanStrip"
