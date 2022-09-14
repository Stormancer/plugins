using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
            if (ctx.State == GameConnectionState.Disconnected)
            {
                closeCts.TrySetResult();
            }
        }

        private TaskCompletionSource closeCts = new TaskCompletionSource();

        public void AddCommandHandler(string commandName, Func<CommandExecutionContext, Task> handler)
        {
            _commandHandlers[commandName] = handler;

        }

        private List<AgentCommandOutputEntryDto>? _pendingOutputs;

        private object syncRoot = new object();

        private void AddOutput(AgentCommandOutputEntryDto entry)
        {
            lock (syncRoot)
            {
                if (_pendingOutputs != null)
                    _pendingOutputs.Add(entry);
            }
        }

        PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        private async Task ConsumePendingOutput()
        {
            while (true)
            {
                try
                {
                    await _timer.WaitForNextTickAsync();

                    lock (syncRoot)
                    {
                        if (_currentContext != null && _pendingOutputs != null)
                        {
                            _currentContext.SendValue(_pendingOutputs);
                            _pendingOutputs.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Error, "consumeLogs", "An error occured while consuming logs", ex);
                }
            }
        }

        private RequestContext<IScenePeer>? _currentContext;
        public async Task Run()
        {
            _ = ConsumePendingOutput();
            await users.Login();



            await users.ConnectToPrivateScene("agents", s =>
            {
                s.AddProcedure("runCommand", async ctx =>
                {
                    lock (syncRoot)
                    {
                        _currentContext = ctx;
                        _pendingOutputs = new List<AgentCommandOutputEntryDto>();
                    }
                    var cmd = ctx.ReadObject<string>();
                    try
                    {


                        var segments = cmd.Split(' ');

                        var name = segments[0];
                        logger.Log(LogLevel.Info, "remoteControl.agent", $"Processing command '{cmd}'");
                        if (_commandHandlers.TryGetValue(name, out var handler))
                        {
                            var commandCtx = new CommandExecutionContext(segments, ctx, dto => AddOutput(dto));

                            await handler(commandCtx);
                            await Task.Delay(500);
                            AddOutput(new AgentCommandOutputEntryDto { Type = "complete", ResultJson = "{}" });
                        }
                        else
                        {
                            AddOutput(new AgentCommandOutputEntryDto { Type = "error", ResultJson = JObject.FromObject(new { error = $"Command '{name}' not supported." }).ToString() });
                        }
                    }
                    catch (Exception ex)
                    {
                        AddOutput(new AgentCommandOutputEntryDto { Type = "error", ResultJson = JObject.FromObject(new { error = ex.ToString() }).ToString() });
                    }

                    bool waitForLogs = true;
                    while (waitForLogs)
                    {
                        lock (syncRoot)
                        {
                            waitForLogs = _pendingOutputs?.Any() ?? false;

                            if (!waitForLogs)
                            {
                                _pendingOutputs = null;
                                _currentContext = null;
                            }

                        }
                        await Task.Delay(1000);

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
