param(
	[Parameter(Mandatory = $true, Position = 0)]
	[string] $FirstImage,

	[Parameter(Mandatory = $true, Position = 1)]
	[string] $SecondImage
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Resolve-ImagePath([string] $Path) {
	if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
		throw "Image not found: $Path"
	}

	return (Resolve-Path -LiteralPath $Path).Path
}

$firstPath = Resolve-ImagePath $FirstImage
$secondPath = Resolve-ImagePath $SecondImage
$first = $null
$second = $null
$output = $null
$graphics = $null

try {
	$first = [System.Drawing.Image]::FromFile($firstPath)
	$second = [System.Drawing.Image]::FromFile($secondPath)

	if ($first.Width -eq $second.Width) {
		throw "The two images have the same width. One must be the wider endcap image and the other the narrower fill image."
	}

	if ($first.Height -ne $second.Height) {
		throw "The images must have the same height. Endcaps: $($first.Width)x$($first.Height); fill: $($second.Width)x$($second.Height)."
	}

	if ($first.Width -gt $second.Width) {
		$endcaps = $first
		$fill = $second
		$endcapPath = $firstPath
		$fillPath = $secondPath
	}
	else {
		$endcaps = $second
		$fill = $first
		$endcapPath = $secondPath
		$fillPath = $firstPath
	}

	if (($endcaps.Width % 2) -ne 0) {
		throw "The wider endcap image must have an even width so it can be split equally: $($endcaps.Width)px."
	}

	$endcapWidth = [int]$endcaps.Width
	$endcapHeight = [int]$endcaps.Height
	$fillWidth = [int]$fill.Width
	$capWidth = [int]($endcapWidth / 2)
	$outputWidth = [int]($endcapWidth + $fillWidth)
	$outputName = "{0}_spliced_{1}.png" -f [System.IO.Path]::GetFileNameWithoutExtension($endcapPath), [System.IO.Path]::GetFileNameWithoutExtension($fillPath)
	$outputPath = Join-Path ([System.IO.Path]::GetDirectoryName($endcapPath)) $outputName

	$output = [System.Drawing.Bitmap]::new($outputWidth, $endcapHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$graphics = [System.Drawing.Graphics]::FromImage($output)
	$graphics.Clear([System.Drawing.Color]::Transparent)
	$leftSource = [System.Drawing.Rectangle]::new(0, 0, $capWidth, $endcapHeight)
	$rightWidth = [int]($endcapWidth - $capWidth)
	$rightSource = [System.Drawing.Rectangle]::new($capWidth, 0, $rightWidth, $endcapHeight)
	$leftDestination = [System.Drawing.Rectangle]::new(0, 0, $capWidth, $endcapHeight)
	$fillDestination = [System.Drawing.Rectangle]::new($capWidth, 0, $fillWidth, $endcapHeight)
	$rightDestination = [System.Drawing.Rectangle]::new($capWidth + $fillWidth, 0, $rightWidth, $endcapHeight)
	$graphics.DrawImage($endcaps, $leftDestination, $leftSource.X, $leftSource.Y, $leftSource.Width, $leftSource.Height, [System.Drawing.GraphicsUnit]::Pixel)
	$graphics.DrawImage($fill, $fillDestination, 0, 0, $fill.Width, $fill.Height, [System.Drawing.GraphicsUnit]::Pixel)
	$graphics.DrawImage($endcaps, $rightDestination, $rightSource.X, $rightSource.Y, $rightSource.Width, $rightSource.Height, [System.Drawing.GraphicsUnit]::Pixel)
	$output.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)

	Write-Host "Created: $outputPath"
	Write-Host "Endcaps: $([System.IO.Path]::GetFileName($endcapPath)) ($($endcaps.Width)x$($endcaps.Height))"
	Write-Host "Fill:    $([System.IO.Path]::GetFileName($fillPath)) ($($fill.Width)x$($fill.Height))"
}
finally {
	if ($graphics) { $graphics.Dispose() }
	if ($output) { $output.Dispose() }
	if ($first) { $first.Dispose() }
	if ($second) { $second.Dispose() }
}
