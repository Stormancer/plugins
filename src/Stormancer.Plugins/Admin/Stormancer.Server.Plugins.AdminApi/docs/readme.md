# Web API integration plugin

This plugin provides helper methods and features for plugins to add ASP.NET Core web Apis to Stormancer applications as Admin web APIs.

## Urls
The Admin APIs are exposed by the cluster behind the following prefix API: `{admin-cluster-endpoint}/_app/{accountId}/{applicationName}/_admin`. 

For instance if the cluster admin endpoint is `https://admin.myserver.com` and the web api route `GET: /myGameData` is declared by the code of the application `my-account/my-app` the final Url of the web API is going to be:

`GET: https://admin.myserver.com/_app/my-account/my-app/_admin/myGameData`

The admin API are secured by the same authentication mechanism used by the cluster admin API, if one is configured on the cluster endpoint. Currently authentication claims cannot be retrieved, but in the future they could be provided as additional headers.

## Exposing new Admin web APis.

The plugin must  expose an implementation of `Stormancer.Server.Plugins.AdminApi.IAdminWebApiConfig` to add the current assembly to the Asp.net application parts list:

    class AdminWebApiConfig : IAdminWebApiConfig
    {
        public void ConfigureApplicationParts(ApplicationPartManager apm)
        {
            apm.ApplicationParts.Add(new AssemblyPart(this.GetType().Assembly));
        }
    }

And register the class in the app dependency injection container when building the plugin. (See `Creating an application plugin` to learn where to add this code.)

    ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
    {
        builder.Register<AdminWebApiConfig>().As<IAdminWebApiConfig>();
    };

Once done, controller in the assembly are automatically detected by the ASP.NET core runtime and exposed as web APIs.


    /// <summary>
    /// Admin web api controller
    /// </summary>
    [Route("baseRoute")]
    public class MyAdminController : ControllerBase
    {
        [HttpGet]
        [Route("test")]
        public IActionResult Test()
        {
            return OK();
        }
    }

Please see the ASP.NET Core MVC documentation to learn more on the framework.