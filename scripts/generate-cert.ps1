Param(
    [string]$Output = "server.pfx",
    [string]$Password = "password"
)

$cert = New-SelfSignedCertificate -DnsName "localhost" -CertStoreLocation "Cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears(5)
$pfxPath = Join-Path -Path (Get-Location) -ChildPath $Output
$bytes = [System.Security.Cryptography.X509Certificates.X509Certificate2]::Export($cert, $Password)
[IO.File]::WriteAllBytes($pfxPath, $bytes)
Write-Output "Wrote $pfxPath"
