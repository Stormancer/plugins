# Analytics

Default ES index: `{account}-{app}-analytics-user`

categories : `login` `sessions` and `logout`

## login

    {
        "type": "user",
        "creationDate": "2023-09-06T18:19:51.431995Z",
        "content": {
            "SessionId": "vLW613-R0kmtDyLHO6OIVg",
            "UserId": "c37b5d76db0d4c6ba4c23f8621f516d2",
            "PlatformId": {
            "Platform": "deviceidentifier",
            "PlatformUserId": "c37b5d76db0d4c6ba4c23f8621f516d2",
            "IsUnknown": false,
            "OnlineId": "c37b5d76db0d4c6ba4c23f8621f516d2"
            },
            "Dimensions": {
            "countryCode": "",
            "continentCode": ""
            }
        },
        "deploymentId": "1bbc2a8a-3ca8-46b9-bba2-d659cc8ef64d",
        "category": "login",
        "accountId": "myaccount",
        "app": "dev",
        "cluster": "mycluster",
        "isDeploymentActive": true
    }
## logout

    {
        "index": null,
        "type": "user",
        "creationDate": "2023-09-06T17:10:27.0587733Z",
        "content": {
          "SessionId": "Sm18j4i0UE--BobzEWaOfg",
          "UserId": "acc2512ce8f14c60ba434db3c050b64a",
          "ConnectedOn": "2023-09-06T16:59:11.9834483Z",
          "duration": 675.075313
        },
        "deploymentId": "1bbc2a8a-3ca8-46b9-bba2-d659cc8ef64d",
        "category": "logout",
        "accountId": "",
        "app": "dev",
        "cluster": "",
        "isDeploymentActive": true
     }


## sessions

    {
        "index": null,
        "type": "user",
        "creationDate": "2023-09-06T18:28:09.5167042Z",
        "content": {
          "AuthenticatedUsersCount": 1
        },
        "deploymentId": "e544ebde-68a6-4ae1-b609-203a3442d63f",
        "category": "sessions",
        "accountId": "",
        "app": "cert-ms",
        "cluster": "",
        "isDeploymentActive": true
     }