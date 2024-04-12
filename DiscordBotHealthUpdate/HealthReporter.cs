﻿using CommonLibrary;
using Discord;
using Discord.WebSocket;
using System.Text.Json;

namespace DiscordBotHealthUpdate
{
    public class HealthReporter
    {
        private static DiscordSocketClient _client;
        private string _token { get; }
        private static HealthResponse? _serverStatus { get; set; } = null;

        public HealthReporter(string token)
        {
            _token = token;
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.Guilds
            });

            _client.Ready += ReadyAsync;
        }

        public async Task Start()
        {
            await _client.LoginAsync(TokenType.Bot, _token).ConfigureAwait(false);
            await _client.StartAsync();

            await Task.Delay(Timeout.Infinite);

        }

        // The Ready event indicates that the client has opened a
        // connection and it is now safe to access the cache.
        private static async Task ReadyAsync()
        {
            Console.WriteLine($"{_client.CurrentUser} is connected!");


            while (true)
            {
                var messages = await GetHealthAsync();
                if (messages.Count != 0)
                {
                    foreach (var guild in _client.Guilds)
                    {
                        var channels = guild.Channels.Where(x => x.GetChannelType() == ChannelType.Text && x.Users.Where(y => y.Id == 1227776913272209478).Any());

                        if (channels.Count() > 1)
                        {
                            var botChannels = channels.Where(x => x.Name.Equals("bot", StringComparison.OrdinalIgnoreCase));
                            if (botChannels.Any())
                                channels = botChannels;
                            else
                            {
                                var generalChannels = channels.Where(x => x.Name.Equals("general", StringComparison.OrdinalIgnoreCase));
                                if (generalChannels.Any())
                                    channels = generalChannels;
                            }

                        }

                        foreach (ITextChannel channel in channels)
                        {
                            foreach (var message in messages)
                            {
                                try
                                {
                                    await channel.SendMessageAsync(message,false, null, new RequestOptions()
                                    {
                                        RetryMode = RetryMode.AlwaysFail
                                    });
                                }
                                catch { }
                            }
                        }
                    }
                }
                await Task.Delay(15000);
            }
        }

        private static async Task<List<string>> GetHealthAsync()
        {
            List<string> messages = new List<string>();
            HttpClient client = new HttpClient();

            var result = await client.GetAsync("http://10.0.0.15:8069/server/health");
            if (result.IsSuccessStatusCode)
            {
                string responsestr = await result.Content.ReadAsStringAsync();
                var jsonObject = JsonSerializer.Deserialize<HealthResponse>(responsestr);

                if (jsonObject != null)
                {
                    if (_serverStatus == null)
                    {
                        _serverStatus = jsonObject;
                    }
                    else
                    {
                        //do comparison
                        List<string> checkedApps = jsonObject.StatusList.Select(x => x.Name).ToList();
                        foreach (var app in _serverStatus.StatusList)
                        {
                            var returnedApp = jsonObject.StatusList.Where(x => x.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase));
                            checkedApps.Remove(app.Name);
                            if (returnedApp.Any() && !returnedApp.First().Status.Equals(app.Status, StringComparison.OrdinalIgnoreCase))
                            {
                                messages.Add($"{returnedApp.First().Name} is {returnedApp.First().Status}");
                            }
                            else if (!returnedApp.Any() && !app.Status.Equals("healthy", StringComparison.OrdinalIgnoreCase))
                            {
                                messages.Add($"{app.Name} is down");
                            }
                        }

                        foreach (var app in checkedApps)
                        {
                            var status = jsonObject.StatusList.Where(x => x.Name.Equals(app, StringComparison.OrdinalIgnoreCase)).First().Status;
                            messages.Add($"{app} is {status}");
                        }

                        _serverStatus.StatusList = jsonObject.StatusList;
                    }
                }
                else
                {
                    messages.Add("server is down");
                }
            }
            else
            {
                messages.Add("server is down");
            }
            client.Dispose();

            return messages;
        }
    }
}
