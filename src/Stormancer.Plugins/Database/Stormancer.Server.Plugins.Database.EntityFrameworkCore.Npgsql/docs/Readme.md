# Npgsql integration

This package configures the EntityFramework core plugin (Stormancer.Server.Plugins.Database.EntityFrameworkCore) to use the open source .NET data provider for PostgreSQL. It allows the app to connect and interact with PostgreSQL servers through Entity framework core.

## Configuration

```
  "postgresql":{
	"host":"myServer",
	"username":"myLogin",
	"passwordPath":"account/secretStore/myPassword",
	"database":"mydatabase"
  }
```