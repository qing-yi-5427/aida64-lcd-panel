param(
    [string]$BackgroundPath = ".\AIDA64-LCD-1200x2608-Chronograph-background.png",
    [string]$OutputPath = ".\AIDA64-LCD-1200x2608-Chronograph.rslcd"
)

$ErrorActionPreference = "Stop"

$ink = 15199985       # #f1eee7
$date = 12041921      # #c1beb7
$amber = 4698879      # #ffb247
$blue = 16754547      # #73a7ff
$track = 2631460      # #242728
$pageBackground = 920843 # #0b0d0e

function New-SimpleItem {
    param(
        [string]$Sensor,
        [int]$Size,
        [int]$Color,
        [string]$Style,
        [int]$X,
        [int]$Y,
        [bool]$ShowUnit = $false,
        [string]$Unit = ""
    )

    $showUnitFlag = if ($ShowUnit) { 1 } else { 0 }
    return " <ID>[SIMPLE]$Sensor</ID><TXTSIZ>$Size</TXTSIZ><FNTNAM>Bahnschrift</FNTNAM><TXTCOL>$Color</TXTCOL><TXTBIR>$Style</TXTBIR><SHWLBL>0</SHWLBL><LBL></LBL><SHWUNT>$showUnitFlag</SHWUNT><UNT>$Unit</UNT><ITMX>$X</ITMX><ITMY>$Y</ITMY>"
}

function New-ArcItem {
    param(
        [string]$Sensor,
        [int]$Color,
        [int]$X,
        [int]$Y
    )

    return " <ID>[ARC]$Sensor</ID><LBL></LBL><DIA>132</DIA><TCK>15</TCK><STAANG>0</STAANG><OPT>0</OPT><BGCOL>0</BGCOL><MIN>0</MIN><LIM1>50</LIM1><LIM2>70</LIM2><LIM3>90</LIM3><MAX>100</MAX><MINFGC>$Color</MINFGC><MINBGC>$track</MINBGC><LIM1FGC>$Color</LIM1FGC><LIM1BGC>$track</LIM1BGC><LIM2FGC>$Color</LIM2FGC><LIM2BGC>$track</LIM2BGC><LIM3FGC>$Color</LIM3FGC><LIM3BGC>$track</LIM3BGC><SHWVAL>0</SHWVAL><TXTSIZ>8</TXTSIZ><FNTNAM>Bahnschrift</FNTNAM><VALCOL>$ink</VALCOL><VALBI>00</VALBI><ITMX>$X</ITMX><ITMY>$Y</ITMY>"
}

function New-BarItem {
    param(
        [string]$Sensor,
        [int]$Color,
        [int]$X,
        [int]$Y,
        [int]$Width
    )

    return " <ID>$Sensor</ID><WID>1</WID><TXTSIZ>8</TXTSIZ><FNTNAM>Bahnschrift</FNTNAM><SHDCOL>$ink</SHDCOL><SHDDIS>0</SHDDIS><SHDDEP>0</SHDDEP><SHWLBL>0</SHWLBL><LBL></LBL><LBLCOL>$ink</LBLCOL><LBLBIS>000</LBLBIS><SHWVAL>0</SHWVAL><VALCOL>$ink</VALCOL><VALBIS>000</VALBIS><SHWUNT>0</SHWUNT><UNT>%</UNT><UNTCOL>$ink</UNTCOL><UNTBIS>000</UNTBIS><UNTWID>1</UNTWID><SHWBAR>1</SHWBAR><BARWID>$Width</BARWID><BARHEI>18</BARHEI><BARIND>0</BARIND><BARPLC>SEP</BARPLC><BARFS>0000</BARFS><BARFRMCOL>0</BARFRMCOL><BARMIN>0</BARMIN><BARLIM1></BARLIM1><BARLIM2></BARLIM2><BARLIM3></BARLIM3><BARMAX>100</BARMAX><BARMINFGC>$Color</BARMINFGC><BARMINBGC>$track</BARMINBGC><BARLIM1FGC>$Color</BARLIM1FGC><BARLIM1BGC>$track</BARLIM1BGC><BARLIM2FGC>$Color</BARLIM2FGC><BARLIM2BGC>$track</BARLIM2BGC><BARLIM3FGC>$Color</BARLIM3FGC><BARLIM3BGC>$track</BARLIM3BGC><ITMX>$X</ITMX><ITMY>$Y</ITMY>"
}

$backgroundFile = (Resolve-Path -LiteralPath $BackgroundPath).Path
$outputFile = [System.IO.Path]::GetFullPath($OutputPath)
$imageBytes = [System.IO.File]::ReadAllBytes($backgroundFile)
$imageHex = [System.Convert]::ToHexString($imageBytes)
$imageName = [System.IO.Path]::GetFileName($backgroundFile)

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("<LCDVER>200</LCDVER><SWVER>6.92.6600</SWVER>")
$lines.Add("<LCDBGCOLOR>$pageBackground</LCDBGCOLOR>")
$lines.Add("<LCDPAGE1>")
$lines.Add(" <URL></URL><ID>IMG</ID><ITMX>0</ITMX><ITMY>0</ITMY><IMGFIL>$imageName</IMGFIL><IMGDAT>$imageHex</IMGDAT>")

# Time and date: the highest-priority information.
$lines.Add((New-SimpleItem -Sensor "STIME" -Size 132 -Color $ink -Style "000" -X 66 -Y 220))
$lines.Add((New-SimpleItem -Sensor "SDATE" -Size 35 -Color $date -Style "001" -X 1132 -Y 332))

# Package/board power: right-aligned immediately before the static W unit.
$lines.Add((New-SimpleItem -Sensor "PCPUPKG" -Size 74 -Color $ink -Style "001" -X 536 -Y 615))
$lines.Add((New-SimpleItem -Sensor "PGPU1" -Size 74 -Color $ink -Style "001" -X 1006 -Y 615))

# CPU and GPU utilization, including compact live arcs.
$lines.Add((New-SimpleItem -Sensor "SCPUUTI" -Size 110 -Color $ink -Style "001" -X 259 -Y 1037))
$lines.Add((New-SimpleItem -Sensor "SGPU1UTI" -Size 110 -Color $ink -Style "001" -X 813 -Y 1037))
$lines.Add((New-ArcItem -Sensor "SCPUUTI" -Color $amber -X 401 -Y 1056))
$lines.Add((New-ArcItem -Sensor "SGPU1UTI" -Color $blue -X 955 -Y 1056))

# Common temperatures, fan speeds, and clocks.
$lines.Add((New-SimpleItem -Sensor "TCPU" -Size 50 -Color $ink -Style "101" -X 502 -Y 1381))
$lines.Add((New-SimpleItem -Sensor "FCPU" -Size 50 -Color $ink -Style "101" -X 474 -Y 1630))
$lines.Add((New-SimpleItem -Sensor "SCPUCLK" -Size 50 -Color $ink -Style "101" -X 475 -Y 1880))
$lines.Add((New-SimpleItem -Sensor "TGPU1" -Size 50 -Color $ink -Style "101" -X 1056 -Y 1381))
$lines.Add((New-SimpleItem -Sensor "FGPU1" -Size 50 -Color $ink -Style "101" -X 1027 -Y 1630))
$lines.Add((New-SimpleItem -Sensor "SGPU1CLK" -Size 50 -Color $ink -Style "101" -X 1029 -Y 1880))

# RAM and VRAM both show used percentage and used capacity.
$lines.Add((New-SimpleItem -Sensor "SMEMUTI" -Size 32 -Color $amber -Style "101" -X 662 -Y 2112 -ShowUnit $true -Unit "%"))
$lines.Add((New-SimpleItem -Sensor "SVMEMUSAGE" -Size 32 -Color $blue -Style "101" -X 1099 -Y 2112 -ShowUnit $true -Unit "%"))
$lines.Add((New-SimpleItem -Sensor "SUSEDMEM" -Size 60 -Color $ink -Style "001" -X 467 -Y 2205 -Unit " GB"))
$lines.Add((New-SimpleItem -Sensor "SGPU1USEDDEMEM" -Size 60 -Color $ink -Style "001" -X 860 -Y 2205 -Unit " GB"))
$lines.Add((New-BarItem -Sensor "SMEMUTI" -Color $amber -X 311 -Y 2373 -Width 352))
$lines.Add((New-BarItem -Sensor "SVMEMUSAGE" -Color $blue -X 747 -Y 2373 -Width 352))

$lines.Add("</LCDPAGE1>")

$content = [string]::Join("`r`n", $lines) + "`r`n"
[System.IO.File]::WriteAllText($outputFile, $content, [System.Text.UTF8Encoding]::new($false))
Write-Output $outputFile
