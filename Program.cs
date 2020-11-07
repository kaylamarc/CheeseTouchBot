// CheeseTouchBot for Discord by Kayla Marcantonio 9/22/2020

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using CronScheduling;

namespace CheeseTouchBot
{
    class Program
    {
        public DiscordClient Client { get; set; }

        /// <summary>
        /// the codeword to contract the cheese touch
        /// </summary>
        public string codeword;

        // list of 100 most common words, all invalid codewords
        public string[] blacklist;

        // list of user entered codewords that can't be used again
        public List<string> userBlacklist;

        // current person with cheese touch disease
        public DiscordMember touched;

        // keeps how long its been since cheese disease has been transferred
        public DateTime touchTime;

        //private static readonly CronDaemon cron_daemon = new CronDaemon();

        // list of server birthdays
        // public List<DateTime> birthdays;

        // ID for CheeseTouch Bot
        public ulong botID;

        // ID for Cheese Touch Role
        public ulong roleID;

        static void Main(string[] args)
        {
            var prog = new Program();
            prog.RunBotAsync().GetAwaiter().GetResult();
        }

        // Birthday Bot feature (TODO)
           // list of birthdays, check if its a persons birthday
           // assign them a birthday role
           // announce them a happy birthday!

        public async Task RunBotAsync()
        {
            StreamReader props = new StreamReader("serverProperties.txt");

            var cfg = new DiscordConfiguration
            {
                Token = props.ReadLine(),
                TokenType = TokenType.Bot,

                AutoReconnect = true,
                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = true
            };

            // first person to say "the" will be the first to get the cheesetouch
            codeword = "the";

            botID = Convert.ToUInt64(props.ReadLine());
            roleID = Convert.ToUInt64(props.ReadLine());

            // read in blacklist
            blacklist = File.ReadAllLines("blacklist.txt");

            // writes user entered blacklist words to userBlacklist.txt
            userBlacklist = File.Exists("userBlacklist.txt") ? File.ReadAllLines("userBlacklist.txt").ToList() : new List<string>();

            // Configure client
            this.Client = new DiscordClient(cfg);
            this.Client.Ready += this.Client_Ready;
            this.Client.MessageCreated += Client_MessageCreated;
            this.Client.GuildAvailable += this.Client_GuildAvailable;
            this.Client.ClientErrored += this.Client_ClientError;

            await this.Client.ConnectAsync();

            await Task.Delay(-1);
        }

        // read all messages sent in server
        private async Task Client_MessageCreated(MessageCreateEventArgs e)
        {
            // handle direct messages
            if (e.Guild == null)
            {
                if (touched != null && e.Author.Equals(touched))
                {
                    // user entered word to be new codeword
                    var word = e.Message.Content;

                    // update the user blacklist if valid word
                    if (updateBlacklist(word))
                    {
                        touchTime = DateTime.Now; // begin time
                        codeword = word.ToLower();
                        await e.Message.RespondAsync("YAY! You sent a valid codeword.");
                    }
                    else
                    {
                        await e.Message.RespondAsync("OOPS! Looks like you sent an invalid word.\nPlease send a new one!");
                    }
                }
                return;
            }

            // force message text lower for checking later
            var msg = e.Message.Content.ToLower();
            var auth = await e.Guild.GetMemberAsync(e.Author.Id);

            // check if message pings bot
            var didPingBot = e.Message.MentionedUsers.Any(u => u.Id == botID);

            // check the message words separated by spaces, 
            // if time has gone over 3 days first person to type will get role or if message pings the bot, 
            // do not give cheesetouch with already cheese touched person
            if ((msg.ToLower().Split(' ').Contains(codeword) || DateTime.Now - touchTime > TimeSpan.FromDays(3) || didPingBot) && !auth.Equals(touched) && !auth.IsBot)
            {
                    // assign cheese touch role (by id)
                await e.Guild.GrantRoleAsync(auth, e.Guild.GetRole(roleID));

                // custom responses
                if (touched == null)
                {
                    await e.Message.RespondAsync($"{e.Author.Mention} TOUCHED _THE CHEESE_ BY SAYING: {codeword}");
                }
                else if (didPingBot)
                {
                    await e.Message.RespondAsync($"{e.Author.Mention} GOT _CHEESED_ BY PINGING ME");
                }
                else
                {
                    await e.Message.RespondAsync($"{e.Author.Mention} TOUCHED _THE CHEESE_ BY SAYING {touched.Mention}'s CODEWORD: {codeword}");
                }

                // direct message new cheese touched person and send them the list of words they cannot use
                if (!didPingBot)
                {
                    var dm = await auth.CreateDmChannelAsync();

                    await dm.SendMessageAsync("YOU HAVE CONTRACTED THE CHEESE TOUCH!\nPlease send me your codeword.\nCodewords must only be one word with **no spaces** and cannot be a word someone else has used.\nInvalid Codewords:");

                    await dm.SendMessageAsync(string.Join(", ", blacklist.Concat(userBlacklist)));
                }

                // assign and revoke roles to cheesers
                if (touched != null)
                {
                    await touched.RevokeRoleAsync(e.Guild.GetRole(roleID));
                }

                // set current touched person and codeword to null
                touched = auth;
                touchTime = DateTime.Now;
                codeword = null;

                return;
            }
        }

        // determines if word is valid (not in the blacklists and has no spaces, nonalphabetics, and ignoring case)
        private bool isValid(string word)
        {
            // returns false if the prospective codeword is in the blacklist, true otherwise
            return !blacklist.Contains(word) && !userBlacklist.Contains(word) && Regex.IsMatch(word, "^[a-z]+$", RegexOptions.IgnoreCase);
        }

        // updates user blacklist and the file if the word is valid, if not return false
        private bool updateBlacklist(string word)
        {
            if (isValid(word))
            {
                userBlacklist.Add(word);
                // write to file
                File.WriteAllLines("userBlacklist.txt", userBlacklist.ToArray());
                return true;
            }
            else
            {
                // invalid word, do not update, try again
                return false;
            }
        }

        private Task Client_Ready(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "CheeseTouchBot", "Client is ready to process events.", DateTime.Now);

            return Task.CompletedTask;
        }

        private Task Client_GuildAvailable(GuildCreateEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "CheeseTouchBot", $"Guild available: {e.Guild.Name}", DateTime.Now);

            return Task.CompletedTask;
        }

        private Task Client_ClientError(ClientErrorEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "CheeseTouchBot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);

            return Task.CompletedTask;
        }
    }
}
