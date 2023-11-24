Provides a factory to create configurable Elasticsearch clients

# Configuration

    {
    ...
        "elasticsearch": {
            //default pattern
            "defaultPattern" : "{account}-{application}-{name}-{type}",
            
            //For debugging, disable direct streaming in the nest client. This enables to see the response contents in ES errors, but reduces performance. Defaults to false
            "DisableDirectStreaming":true,

            "indices":{
                "my-name":{
                
                // Retry timeout in seconds.
                "retryTimeout" : 30,

              
                // Max number of retries before failure.
                "maxRetries": 5,
               
                // Ping timeout (ms)
                "pingTimeout" :2000,

                // Pattern used to transform the app index into an ES index.
                "pattern":"",

                /// Connection pool to use to interact with the index.
                "onnectionPool": "default"
                }
            }


            //Configuration the connection pools. By default, all indices use the default pool.
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