Write-Output "==== Deploy application ===="
dotnet tool run stormancer manage app deploy --app ".\stormancer-dev.profile.json" --create --configure --deploy
