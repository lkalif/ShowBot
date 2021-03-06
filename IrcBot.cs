﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using Meebey.SmartIrc4net;

namespace ShowBot
{
    #region Helper and data model classes
    public class IrcServerInfo
    {
        public string ServerID;
        public string ServerHost;
        public int ServerPort;
        public bool ServerSSL;
        public string Nick;
        public string Username;
        public string RealName;
        public string Password;
        public string WebBase;
        public string ApiAuth;
        public List<string> Channels;
        public List<string> AdditionalOps;

        public IrcServerInfo()
        {
            ServerPort = 6667;
            Channels = new List<string>();
            AdditionalOps = new List<string>();
            RealName = Password = string.Empty;
        }
    }

    public class ResponseStatus
    {
        public bool Success;
        public string Message;
    }

    public class Request
    {
        public string ApiAuth;
        public string Function;
        public string ServerID;
        public string Channel;
        public string Title;
    }

    public class Sugggestion : Request
    {
        public string User;
        public string Title;
    }

    #endregion Helper and data model classes

    public class IrcBot : IDisposable
    {
        public IrcServerInfo Conf;
        Thread IRCConnection;

        public IrcClient irc;

        bool SentNickServLogin = false;

        static Regex ChanCmdParser = new Regex(@"^!(?<cmd>\w+)\s*(?<arg>.+)?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        static Regex QueryCmdParser = new Regex(@"^\s*!?(?<cmd>\w+)\s*(?<arg>.+)?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        HttpClient WebClient;

        public IrcBot(IrcServerInfo c)
        {
            Conf = c;

            irc = new IrcClient();
            irc.SendDelay = 200;
            irc.AutoReconnect = true;
            irc.AutoRejoin = true;
            irc.AutoRejoinOnKick = false;
            irc.AutoRelogin = true;
            irc.AutoRetry = true;
            irc.UseSsl = Conf.ServerSSL;
            irc.ValidateServerCertificate = false;
            irc.AutoRetryDelay = 30;
            irc.CtcpVersion = "ShowBot by lkalif 1.0";
            irc.Encoding = Encoding.UTF8;
            irc.AutoNickHandling = true;
            irc.ActiveChannelSyncing = true;

            WebClient = new HttpClient();
            WebClient.BaseAddress = new Uri(Conf.WebBase);
            WebClient.DefaultRequestHeaders.Accept.Clear();
            WebClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            irc.OnRawMessage += irc_OnRawMessage;
            irc.OnJoin += irc_OnJoin;
            irc.OnConnected += irc_OnConnected;
            irc.OnChannelMessage += irc_OnChannelMessage;
            irc.OnQueryMessage += irc_OnQueryMessage;
        }

        public void Dispose()
        {
            try
            {
                if (irc != null)
                {
                    if (irc.IsConnected)
                    {
                        irc.RfcDie();
                        irc.Disconnect();
                    }
                    irc = null;
                }

                if (IRCConnection != null)
                {
                    if (IRCConnection.IsAlive)
                        IRCConnection.Abort();
                    IRCConnection = null;
                }
            }
            catch { }
        }

        List<string> SplitMessage(string message)
        {
            List<string> ret = new List<string>();
            string[] lines = Regex.Split(message, "\n+");

            for (int l = 0; l < lines.Length; l++)
            {

                string[] words = lines[l].Split(' ');
                string outstr = string.Empty;

                for (int i = 0; i < words.Length; i++)
                {
                    outstr += words[i] + " ";
                    if (outstr.Length > 380)
                    {
                        ret.Add(outstr.Trim());
                        outstr = string.Empty;
                    }
                }
                ret.Add(outstr.Trim());
            }

            return ret;
        }

        void irc_OnConnected(object sender, EventArgs e)
        {
            PrintMsg("IRC - " + Conf.ServerID, string.Format("Connected"));
        }

        async Task<ResponseStatus> SendSuggestion(Sugggestion s)
        {
            try
            {
                var response = await WebClient.PostAsJsonAsync<Sugggestion>("api.php", s);
                if (response.IsSuccessStatusCode)
                {
                    ResponseStatus res = await response.Content.ReadAsAsync<ResponseStatus>();
                    if (res == null)
                    {
                        throw new InvalidOperationException("Failed to deserialize the result");
                    }
                    return res;
                }
            }
            catch (Exception ex)
            {
                PrintMsg("HTTP Error", ex.Message);
            }
            return new ResponseStatus() { Success = false, Message = "HTTP request failed" };
        }

        async void irc_OnChannelMessage(object sender, IrcEventArgs e)
        {
            try
            {
                Match m = ChanCmdParser.Match(e.Data.Message);
                if (m.Success)
                {
                    string cmd = m.Groups["cmd"].Value;
                    string arg = m.Groups["arg"].Value;

                    switch (cmd)
                    {
                        case "s":
                            var req = new Sugggestion()
                            {
                                ApiAuth = Conf.ApiAuth,
                                Function = "suggestion_add",
                                ServerID = Conf.ServerID,
                                Channel = e.Data.Channel,
                                User = e.Data.Nick,
                                Title = arg
                            };

                            var res = await SendSuggestion(req);
                            irc.SendMessage(SendType.Message, e.Data.Nick, "Sending your suggestion " + (res.Success ? "was successful" : "failed") + ": " + res.Message);
                            break;

                        case "start":
                        case "reset":
                            break;
                    }
                }
            }
            catch { }
        }

        async void irc_OnQueryMessage(object sender, IrcEventArgs e)
        {
            try
            {
                Match m = QueryCmdParser.Match(e.Data.Message);
                if (m.Success)
                {
                    string cmd = m.Groups["cmd"].Value;
                    string[] args = Regex.Split(m.Groups["arg"].Value, @"\s+");

                    switch (cmd)
                    {
                        case "s":
                        case "suggest":
                            {
                                if (args.Length < 2)
                                {
                                    irc.SendReply(e.Data, "Usage: " + cmd + " #channel some suggestion here");
                                    return;
                                }

                                var title = string.Join(" ", new ArraySegment<string>(args, 1, args.Length - 1));

                                var req = new Sugggestion()
                                {
                                    ApiAuth = Conf.ApiAuth,
                                    Function = "suggestion_add",
                                    ServerID = Conf.ServerID,
                                    Channel = args[0],
                                    User = e.Data.Nick,
                                    Title = title
                                };

                                var res = await SendSuggestion(req);
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Sending your suggestion " + (res.Success ? "was successful" : "failed") + ": " + res.Message);
                            }
                            break;
                            
                        case "delete":
                            {
                                if (args.Length < 2 || string.IsNullOrEmpty(args[0]))
                                {
                                    irc.SendReply(e.Data, "Usage: " + cmd + " #channel title - delete title");
                                    return;
                                }

                                if (!Conf.AdditionalOps.Contains(e.Data.Nick))
                                {
                                    var user = irc.GetChannelUser(args[0], e.Data.Nick);
                                    if (user == null)
                                    {
                                        irc.SendReply(e.Data, "Cannot find you in that channel");
                                        return;
                                    }

                                    if (!(user.IsIrcOp || user.IsOp))
                                    {
                                        irc.SendReply(e.Data, "You need to be a channel operator on " + args[0]);
                                        return;
                                    }
                                }

                                var title = m.Groups["arg"].Value;
                                var t = Regex.Match(title, @"^.+? (?<title>.+)");
                                
                                if (t.Success)
                                {
                                    title = t.Groups["title"].Value;
                                }

                                var req = new Request()
                                {
                                    ApiAuth = Conf.ApiAuth,
                                    Function = "title_delete",
                                    ServerID = Conf.ServerID,
                                    Channel = args[0],
                                    Title = title,
                                };

                                try
                                {
                                    var response = await WebClient.PostAsJsonAsync<Request>("api.php", req);
                                    ResponseStatus res = await response.Content.ReadAsAsync<ResponseStatus>();
                                    irc.SendReply(e.Data, "Sending delete request " + (res.Success ? "was successful" : "failed") + ": " + res.Message);
                                }
                                catch (Exception ex)
                                {
                                    irc.SendReply(e.Data, "Failed sending reset request: " + ex.Message);
                                    return;
                                }
                            }
                            break;


                        case "start":
                        case "reset":
                            {
                                if (args.Length != 1 || string.IsNullOrEmpty(args[0]))
                                {
                                    irc.SendReply(e.Data, "Usage: " + cmd + " #channel - delete all titles and votes for the title suggestion on #channel");
                                    return;
                                }

                                if (!Conf.AdditionalOps.Contains(e.Data.Nick))
                                {
                                    var user = irc.GetChannelUser(args[0], e.Data.Nick);
                                    if (user == null)
                                    {
                                        irc.SendReply(e.Data, "Cannot find you in that channel");
                                        return;
                                    }

                                    if (!(user.IsIrcOp || user.IsOp))
                                    {
                                        irc.SendReply(e.Data, "You need to be a channel operator on " + args[0]);
                                        return;
                                    }
                                }

                                var req = new Request()
                                {
                                    ApiAuth = Conf.ApiAuth,
                                    Function = "channel_reset",
                                    ServerID = Conf.ServerID,
                                    Channel = args[0],
                                };

                                try
                                {
                                    var response = await WebClient.PostAsJsonAsync<Request>("api.php", req);
                                    ResponseStatus res = await response.Content.ReadAsAsync<ResponseStatus>();
                                    irc.SendReply(e.Data, "Sending reset request " + (res.Success ? "was successful" : "failed") + ": " + res.Message);
                                }
                                catch (Exception ex)
                                {
                                    irc.SendReply(e.Data, "Failed sending reset request: " + ex.Message);
                                    return;
                                }
                            }
                            break;

                        case "top":
                            {
                                if (args.Length != 1 || string.IsNullOrEmpty(args[0]))
                                {
                                    irc.SendReply(e.Data, "Usage: " + cmd + " #channel - get the top 5 suggestions for #channel");
                                    return;
                                }

                                var req = new Request()
                                {
                                    ApiAuth = Conf.ApiAuth,
                                    Function = "channel_top",
                                    ServerID = Conf.ServerID,
                                    Channel = args[0],
                                };

                                try
                                {
                                    var response = await WebClient.PostAsJsonAsync<Request>("api.php", req);
                                    var resp = await response.Content.ReadAsStringAsync();
                                    ResponseStatus res = await response.Content.ReadAsAsync<ResponseStatus>();
                                    if (res.Success)
                                    {
                                        foreach (var line in SplitMessage(res.Message))
                                        {
                                            irc.SendReply(e.Data, line);
                                        }
                                    }
                                    else
                                    {
                                        irc.SendReply(e.Data, "Failed to get Top 5: " + res.Message);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    irc.SendReply(e.Data, "Failed sending reset request: " + ex.Message);
                                    return;
                                }
                            }

                            break;

                        case "help":
                            irc.SendReply(e.Data, "ShowBot by lkalif:\n");
                            irc.SendReply(e.Data, "  suggest #channel some title suggestion for channel");
                            irc.SendReply(e.Data, "  top #channel - displays current top 5 on #channel");
                            irc.SendReply(e.Data, "  reset #channel - delete all suggestion and votes for #channel");
                            irc.SendReply(e.Data, "  delete #channel title - delete title from #channel");
                            irc.SendReply(e.Data, "     reset requires you to be an operator of #channel");
                            irc.SendReply(e.Data, "  help - Displays this text");
                            break;

                        default:
                            irc.SendReply(e.Data, "Unknown command. Try help");
                            break;
                    }
                }
            }
            catch { }
        }

        void irc_OnJoin(object sender, JoinEventArgs e)
        {
            PrintMsg("IRC - " + Conf.ServerID, string.Format("{1} joined {0}", e.Channel, e.Who));
        }

        void irc_OnRawMessage(object sender, IrcEventArgs e)
        {
            if (e.Data.Type == ReceiveType.Unknown) return;
            PrintMsg(e.Data.Nick, string.Format("({0}) {1}", e.Data.Type, e.Data.Message));
            try
            {
                if (!string.IsNullOrEmpty(e.Data.Nick) &&
                    e.Data.Nick.ToLower() == "nickserv" &&
                    !SentNickServLogin && !string.IsNullOrEmpty(Conf.Password))
                {
                    SentNickServLogin = true;
                    irc.SendMessage(SendType.Message, "nickserv", "identify " + Conf.Password);
                }
            }
            catch { }
        }

        void PrintMsg(string from, string msg)
        {
            Console.WriteLine("{0}: {1}", from, msg);
        }

        public void Connect()
        {
            if (IRCConnection != null)
            {
                if (IRCConnection.IsAlive)
                    IRCConnection.Abort();
                IRCConnection = null;
            }

            IRCConnection = new Thread(new ThreadStart(IrcThread));
            IRCConnection.Name = "IRC Thread";
            IRCConnection.IsBackground = true;
            IRCConnection.Start();
        }

        private void IrcThread()
        {
            PrintMsg("IRC - " + Conf.ServerID, "Connecting...");

            try
            {
                irc.Connect(Conf.ServerHost, Conf.ServerPort);
                PrintMsg("System", "Logging in...");
                if (string.IsNullOrEmpty(Conf.Password))
                {
                    irc.Login(Conf.Nick, Conf.RealName, 0, Conf.Username);
                }
                else
                {
                    irc.Login(Conf.Nick, Conf.RealName, 0, Conf.Username, Conf.Password);
                }
                foreach (var chan in Conf.Channels)
                {
                    irc.RfcJoin(chan);
                }
            }
            catch (Exception ex)
            {
                PrintMsg("System", "An error has occured: " + ex.Message);
            }

            try
            {
                irc.Listen();
                if (irc.IsConnected)
                {
                    irc.AutoReconnect = false;
                    irc.RfcDie();
                    irc.Disconnect();
                }
            }
            catch { }
        }
    }
}

