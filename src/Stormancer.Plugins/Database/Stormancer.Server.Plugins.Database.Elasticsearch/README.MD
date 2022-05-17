# Secrets

## Password

Format : string

```ps
Invoke-RestMethod -Method Put -Uri "$baseUri/_secrets/[GameOrPublisherName]/secrets/elasticSearch_password" -ContentType "text/plain" -InFile "$PSScriptRoot\Secrets\elasticSearch_password.txt"
```

[GameOrPublisherName] can be replaced by the Game or Publisher Stormancer account name.
