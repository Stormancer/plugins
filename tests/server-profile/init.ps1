$baseUri = "http://stormancer-1.stormancer.com:9080"
$deployConfigFile = "stormancer-dev.profile.json"

function ensureSuccessStatusCode
{
    param ($response)
    if (! (($response.StatusCode -ge 200) -and ($response.StatusCode -lt 300)))
    {
        Write-Output "Failed : ${response.StatusCode} ${response.StatusDescription}"
        Write-Host $response
        Exit
    }
    else
    {
        Write-Output $response.StatusDescription
    }
}

Write-Output "==== Create new tool manifest ===="
dotnet new tool-manifest

Write-Output "==== Install Stormancer.CLI ===="
dotnet tool install Stormancer.CLI

Write-Output "==== Install Stormancer new config ===="
dotnet tool run stormancer new config

Write-Output "==== Install Stormancer plugins ===="
dotnet tool run stormancer plugins add --id Stormancer.Server.Node
dotnet tool run stormancer plugins add --id Stormancer.Logging.Nlog
dotnet tool run stormancer plugins add --id Stormancer.Management.CLI
dotnet tool run stormancer plugins add --id Stormancer.Elasticsearch
dotnet tool run stormancer plugins add --id Stormancer.Diagnostics

Write-Output "==== Add Stormancer cluster ===="
dotnet tool run stormancer manage clusters add --endpoint "$baseUri"

try
{
    Write-Output "==== Extract account name ===="
    $envConfig = Get-Content "$deployConfigFile" | ConvertFrom-Json
    $account = $envConfig.Account

}
catch
{
    Write-Output "Error: Can't read deployConfigFile '$deployConfigFile'"
    return 1
}

Write-Output "==== Create account ===="
$response = Invoke-WebRequest -Method Put -Uri "$baseUri/_account/$account" -ContentType "application/json" -Body "{}"
ensureSuccessStatusCode($response)
