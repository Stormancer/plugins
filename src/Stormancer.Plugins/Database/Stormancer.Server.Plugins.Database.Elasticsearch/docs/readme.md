# Configuration

    {
    ...
        "elasticsearch": {
            //For debugging, disable direct streaming in the nest client. This enables to see the response contents in ES errors, but reduces performance. Defaults to false
            "DisableDirectStreaming":true,
            "connectionPools": {
                "default": {
                    "Sniffing": false,
                    "Endpoints": [
                        "https://x:9200",
                        "https://y:9200",
                        "https://z:9200"
                    ],
                    //If the certificates of the Elasticsearch cluster are self signed
                    "IgnoreCertificateErrors": true,
                    "Credentials": {
                        "Basic": {
                            "Login": "my-app-login",
                            //Path to the password in the cluster's secrets stores.
                            "PasswordPath": "my-account/my-secret-store-id/elasticSearch_password"
                        }
                    }
                }
            }
        },
    }