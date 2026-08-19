# Генератор иконок ленты для AbrPlanStrip. По образцу civil3d/lisp/icons/make-icons.ps1.
#
# Конвенция AutoCAD: PNG с альфой, 16x16 и 32x32, отдельные наборы под тёмную
# и светлую тему. Поля вокруг глифа обязательны - без них рисунок упирается
# в край кнопки и выглядит обрезанным.
#
# Рисуем с восьмикратным запасом и уменьшаем бикубикой: сглаживание получается
# ровным на обоих размерах без ручной подгонки по пикселям.

Add-Type -AssemblyName System.Drawing

$OutDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
$Scale = 8

$Themes = @{
    light = @{ Neutral = '#FF414141'; Accent = '#FF0E7C93' }
    dark  = @{ Neutral = '#FFD4D9DC'; Accent = '#FF3FC7E3' }
}

function Get-Color([string]$argb) {
    return [System.Drawing.ColorTranslator]::FromHtml('#' + $argb.Substring(3))
}

function New-Pen($color, [double]$widthPx) {
    $pen = New-Object System.Drawing.Pen($color, [float]($widthPx * $Scale))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Draw-PlanStripBuild($g, [int]$S, $neutral, $accent, [double]$stroke) {
    # Кривая трасса слева выпрямляется в прямую полосу справа - суть модуля.
    $penNeutral = New-Pen $neutral $stroke
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddBezier(
        [float]($S * 0.12), [float]($S * 0.78),
        [float]($S * 0.30), [float]($S * 0.20),
        [float]($S * 0.55), [float]($S * 0.75),
        [float]($S * 0.88), [float]($S * 0.22))
    $g.DrawPath($penNeutral, $path)
    $path.Dispose()

    $penAccent = New-Pen $accent $stroke
    $y = [float]($S * 0.60)
    $g.DrawLine($penAccent, [float]($S * 0.14), $y, [float]($S * 0.86), $y)

    foreach ($x in @(0.30, 0.50, 0.70)) {
        $gx = [float]($S * $x)
        $g.DrawLine($penAccent, $gx, $y - [float]($S * 0.06), $gx, $y + [float]($S * 0.06))
    }

    $penAccent.Dispose(); $penNeutral.Dispose()
}

function Draw-PlanStripUpdate($g, [int]$S, $neutral, $accent, [double]$stroke) {
    $pen = New-Pen $accent $stroke
    $cap = New-Object System.Drawing.Drawing2D.AdjustableArrowCap([float]2.2, [float]2.2, $true)
    $pen.CustomEndCap = $cap

    $rect = New-Object System.Drawing.RectangleF(
        [float]($S * 0.18), [float]($S * 0.18), [float]($S * 0.64), [float]($S * 0.64))
    $g.DrawArc($pen, $rect, 20, 300)

    $cap.Dispose(); $pen.Dispose()
}

function Draw-PlanStripErase($g, [int]$S, $neutral, $accent, [double]$stroke) {
    $penNeutral = New-Pen $neutral $stroke
    $rect = New-Object System.Drawing.RectangleF(
        [float]($S * 0.14), [float]($S * 0.34), [float]($S * 0.72), [float]($S * 0.32))
    $g.DrawRectangle($penNeutral, $rect.X, $rect.Y, $rect.Width, $rect.Height)
    $penNeutral.Dispose()

    $penAccent = New-Pen $accent $stroke
    $g.DrawLine($penAccent, [float]($S * 0.20), [float]($S * 0.20), [float]($S * 0.80), [float]($S * 0.80))
    $g.DrawLine($penAccent, [float]($S * 0.80), [float]($S * 0.20), [float]($S * 0.20), [float]($S * 0.80))
    $penAccent.Dispose()
}

function Draw-PlanStripSettings($g, [int]$S, $neutral, $accent, [double]$stroke) {
    # Три ползунка - пресеты настроек.
    $penNeutral = New-Pen $neutral $stroke
    $penAccent = New-Pen $accent $stroke
    $brush = New-Object System.Drawing.SolidBrush($accent)

    $rows = @(
        @(0.26, 0.62),
        @(0.50, 0.38),
        @(0.74, 0.70)
    )

    foreach ($row in $rows) {
        $y = [float]($S * $row[0])
        $knob = [float]($S * $row[1])
        $g.DrawLine($penNeutral, [float]($S * 0.14), $y, [float]($S * 0.86), $y)

        $r = [float]($stroke * $Scale * 0.85)
        $g.FillEllipse($brush, $knob - $r, $y - $r, $r * 2, $r * 2)
        $g.DrawEllipse($penAccent, $knob - $r, $y - $r, $r * 2, $r * 2)
    }

    $brush.Dispose(); $penAccent.Dispose(); $penNeutral.Dispose()
}

function Draw-PlanStripAbout($g, [int]$S, $neutral, $accent, [double]$stroke) {
    # "i" в кружке - стандартный глиф "о модуле" по всей линейке.
    $penNeutral = New-Pen $neutral $stroke
    $rect = New-Object System.Drawing.RectangleF(
        [float]($S * 0.14), [float]($S * 0.14), [float]($S * 0.72), [float]($S * 0.72))
    $g.DrawEllipse($penNeutral, $rect)

    $penAccent = New-Pen $accent $stroke
    $x = [float]($S * 0.5)
    $g.DrawLine($penAccent, $x, [float]($S * 0.46), $x, [float]($S * 0.70))

    $dot = [float]($stroke * $Scale * 0.62)
    $brush = New-Object System.Drawing.SolidBrush($accent)
    $g.FillEllipse($brush, $x - $dot / 2, [float]($S * 0.30) - $dot / 2, $dot, $dot)

    $brush.Dispose(); $penAccent.Dispose(); $penNeutral.Dispose()
}

function Write-Icon([string]$name, [int]$size, [string]$theme) {
    $S = $size * $Scale
    $big = New-Object System.Drawing.Bitmap($S, $S,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $g = [System.Drawing.Graphics]::FromImage($big)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $neutral = Get-Color $Themes[$theme].Neutral
    $accent = Get-Color $Themes[$theme].Accent
    $stroke = if ($size -le 16) { 1.7 } else { 2.3 }

    switch ($name) {
        'planstrip_build'    { Draw-PlanStripBuild    $g $S $neutral $accent $stroke }
        'planstrip_update'   { Draw-PlanStripUpdate   $g $S $neutral $accent $stroke }
        'planstrip_erase'    { Draw-PlanStripErase    $g $S $neutral $accent $stroke }
        'planstrip_settings' { Draw-PlanStripSettings $g $S $neutral $accent $stroke }
        'planstrip_about'    { Draw-PlanStripAbout    $g $S $neutral $accent $stroke }
        default               { throw "Неизвестная иконка: $name" }
    }
    $g.Dispose()

    $small = New-Object System.Drawing.Bitmap($size, $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gs = [System.Drawing.Graphics]::FromImage($small)
    $gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gs.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $gs.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $gs.Clear([System.Drawing.Color]::Transparent)
    $gs.DrawImage($big, 0, 0, $size, $size)
    $gs.Dispose()

    $path = Join-Path $OutDir ("{0}_{1}_{2}.png" -f $name, $size, $theme)
    $small.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)

    $small.Dispose(); $big.Dispose()
    Write-Host ("  {0}" -f [System.IO.Path]::GetFileName($path))
}

Write-Host "Генерация иконок в $OutDir"
foreach ($name in @('planstrip_build', 'planstrip_update', 'planstrip_erase', 'planstrip_settings', 'planstrip_about')) {
    foreach ($size in @(16, 32)) {
        foreach ($theme in @('light', 'dark')) {
            Write-Icon $name $size $theme
        }
    }
}
Write-Host "Готово."
