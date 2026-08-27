# Extracts the conceptual (CSDL), storage (SSDL) and mapping (MSL) sections
# from TinyCrmModel.edmx into the separate files that are embedded as
# assembly resources at build time.
#
# Run this after editing TinyCrmModel.edmx (e.g. in the Visual Studio
# EDMX designer), so the embedded artifacts stay in sync with the model.

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$edmxPath = Join-Path $here 'TinyCrmModel.edmx'

[xml]$edmx = Get-Content $edmxPath -Encoding UTF8

$ns = New-Object System.Xml.XmlNamespaceManager($edmx.NameTable)
$ns.AddNamespace('edmx', 'http://schemas.microsoft.com/ado/2009/11/edmx')

$sections = @(
    @{ XPath = '/edmx:Edmx/edmx:Runtime/edmx:ConceptualModels/*'; Out = 'TinyCrmModel.csdl' },
    @{ XPath = '/edmx:Edmx/edmx:Runtime/edmx:StorageModels/*';   Out = 'TinyCrmModel.ssdl' },
    @{ XPath = '/edmx:Edmx/edmx:Runtime/edmx:Mappings/*';        Out = 'TinyCrmModel.msl'  }
)

foreach ($section in $sections) {
    $node = $edmx.SelectSingleNode($section.XPath, $ns)
    if ($null -eq $node) { throw "Section not found in EDMX: $($section.XPath)" }
    $outPath = Join-Path $here $section.Out
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.OmitXmlDeclaration = $false
    $writer = [System.Xml.XmlWriter]::Create($outPath, $settings)
    try { $node.WriteTo($writer) } finally { $writer.Close() }
    Write-Host "Wrote $outPath"
}
