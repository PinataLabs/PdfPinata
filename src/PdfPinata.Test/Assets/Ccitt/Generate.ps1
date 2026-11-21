# Writes the CCITT test vectors: text.pbm, a line of text as a 1-bit scan, and one encoding of it
# per parameter set in encode.ps, each made by Ghostscript's CCITTFaxEncode rather than by
# anything in this repository. Needs ImageMagick and Ghostscript on the path.
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    magick -size 301x61 xc:white -fill black -font Arial -pointsize 30 -annotate +6+32 'CCITT Group 4' `
        -draw 'rectangle 0,48 300,50' -draw 'circle 280,15 290,15' -threshold 50% text.pbm
    gs -q -dNOSAFER -dNOPAUSE -dBATCH -sDEVICE=nullpage encode.ps
} finally {
    Pop-Location
}

# A page scanned at 150 dots to the inch, skewed and speckled as a scanner leaves one, stored as
# scan.pbm and as the Group 4 PDF ImageMagick writes from it.
Push-Location $PSScriptRoot
try {
    magick -size 600x400 xc:white -fill black -font Times-New-Roman -pointsize 26 `
        -annotate +30+60 'Facsimile transmission' -pointsize 18 `
        -annotate +30+110 'Pages scanned at 150 dots to the inch arrive' `
        -annotate +30+140 'as bilevel images coded with ITU-T T.6, which' `
        -annotate +30+170 'PDF stores under the CCITTFaxDecode filter.' `
        -draw 'rectangle 30,200 570,204' -draw 'rectangle 30,230 230,360' `
        -rotate 1.2 -gravity center -extent 600x400 -attenuate 0.15 +noise Impulse -threshold 60% scan.pbm
    magick scan.pbm -density 150 -units PixelsPerInch -compress Group4 group4-scan.pdf
} finally {
    Pop-Location
}
