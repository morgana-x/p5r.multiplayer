# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/p5r.code.multiplayerclient/*" -Force -Recurse
dotnet publish "./p5r.code.multiplayerclient.csproj" -c Release -o "$env:RELOADEDIIMODS/p5r.code.multiplayerclient" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location