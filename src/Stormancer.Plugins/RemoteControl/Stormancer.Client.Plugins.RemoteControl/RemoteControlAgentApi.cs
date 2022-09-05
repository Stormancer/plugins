using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Plugins.RemoteControl
{
    public class RemoteControlAgentApi
    {
        private readonly Dictionary<string, Func<CommandExecutionContext, Task>> _commandHandlers = new Dictionary<string, Func<CommandExecutionContext, Task>>();
        private readonly UserApi users;
        private readonly ILogger logger;

        internal RemoteControlAgentApi(Plugins.UserApi users, ILogger logger)
        {
            this.users = users;
            this.logger = logger;
            users.OnGameConnectionStateChanged += OnConnectionStateChanged;
        }
        private void OnConnectionStateChanged(GameConnectionStateCtx ctx)
        {
            if(ctx.State == GameConnectionState.Disconnected)
            {
                closeCts.TrySetResult();
            }
        }

        private TaskCompletionSource closeCts = new TaskCompletionSource();

        public void AddCommandHandler(string commandName, Func<CommandExecutionContext, Task> handler)
        {
            _commandHandlers[commandName] = handler;

        }
        public async Task Run()
        {
           
            await users.Login();

            await users.ConnectToPrivateScene("agents", s =>
            {
                s.AddProcedure("runCommand", async ctx =>
                {
                    var cmd = ctx.ReadObject<string>();
                    try
                    {
                        

                        var segments = cmd.Split(' ');

                        var name = segments[0];
                        logger.Log(LogLevel.Info, "remoteControl.agent", $"Processing command '{cmd}'");
                        if (_commandHandlers.TryGetValue(name, out var handler))
                        {
                            var commandCtx = new CommandExecutionContext(segments,ctx);

                            await handler(commandCtx);
                            await Task.Delay(500);
                            ctx.SendValue(new AgentCommandOutputEntryDto { Type = "complete", ResultJson = "{}" });
                        }
                        else
                        {
                            ctx.SendValue(new AgentCommandOutputEntryDto { Type = "error", ResultJson = JObject.FromObject(new { error = $"Command '{name}' not supported." }).ToString() });
                        }
                    }
                    catch(Exception ex)
                    {
                        ctx.SendValue(new AgentCommandOutputEntryDto { Type = "error", ResultJson = JObject.FromObject(new { error = ex.ToString()  }).ToString() });
                    }
                    logger.Log(LogLevel.Info, "remoteControl.agent", $"completed command '{cmd}'");

                });
            });

            logger.Log(LogLevel.Info, "remoteControl.agent", "Waiting for commands...");

            await closeCts.Task;
        }
    }

    public class AgentCommandOutputEntryDto
    {
        public string Type { get; set; }
        public string ResultJson { get; set; }
    }

}
