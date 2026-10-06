# OAuth identity support

## Configuration

```
{
    "users":{
        "oAuth":{
            // path to the blob in the secret store containing the valid signing keys.
            "keysPath":{pathToSecretStore}
            "activeKey":{keyId}
            "issuer":"mygame",
            "validAudiences":[...]
        }
    }
}
```

## Keys blob
The keys blob contains private keys used to sign JWT created by the application.

```
{
    "keys":[{jwk formatted private keys}]
}
```


## Creating private keys

The application provides an admin API to easily create new RSA signing keys.

```
Request:

POST: /OAuthAdmin/
{
    "keyId":{keyName}
}


Response:
{jwk}
```