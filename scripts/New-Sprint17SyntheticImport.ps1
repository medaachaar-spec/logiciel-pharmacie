[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateRange(1, 100000)]
    [int]$Rows,

    [Parameter(Mandatory)]
    [string]$OutputFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([IO.Path]::GetExtension($OutputFile) -ne '.csv') { throw 'La sortie doit être un fichier .csv.' }
$lines = [Collections.Generic.List[string]]::new($Rows + 1)
$lines.Add('Reference;Designation;PrixVente;CodeBarres;Categorie')
for ($index = 1; $index -le $Rows; $index++) {
    $barcode = ("67{0:00000000000}" -f $index)
    $lines.Add(("S17-BULK-{0:00000};Produit synthétique {0};12.50;{1};GEN" -f $index, $barcode))
}
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($OutputFile), $lines, [Text.UTF8Encoding]::new($false))
Get-Item -LiteralPath $OutputFile | Select-Object FullName, Length
