using System;
using System.Text;
using System.Collections.Generic;
using XCommas.Net.Objects;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace fomodealer
{
    class Program
    {
        static string key = "xxx";
        static string secret = "xxx";

        static void IncrementDeal( Dictionary<string, int> deals, string pair)
        {
            if (!deals.ContainsKey(pair)) deals.Add(pair, 1);
            else deals[pair]++;
        }

        static void Main(string[] args)
        {
            DiscordWrapper dw = new DiscordWrapper("xxx");

            bool firstRun = true; //don't add deals on the first run because they're not 'new' necessarily

            var api = new XCommas.Net.XCommasApi(key, secret, default, UserMode.Paper); //can be paper account
            Dictionary<int, bool> lookup = new Dictionary<int, bool>(); //deal Ids lookup
            while (true)
            {
                var deals = api.GetDealsAsync(limit: 1500, dealScope: DealScope.Completed, dealOrder: DealOrder.ClosedAt).Result; //limit: 1000, 
                bool hasPlayed = false;
                Console.Clear();
                StringBuilder sb = new StringBuilder();

                Dictionary<string, int> deals30m = new Dictionary<string, int>(),
                     deals1hr = new Dictionary<string, int>(),
                     //deals2hr = new Dictionary<string, int>(),
                     deals4hr = new Dictionary<string, int>();
                foreach (Deal deal in deals.Data)
                {
                    string pair = deal.Pair.Replace("USDT_", "");
                    DateTime ClosedAt = (DateTime)deal.ClosedAt;
                    double totalMin = DateTime.UtcNow.Subtract(ClosedAt).TotalMinutes;
                    if (totalMin <= 30) IncrementDeal(deals30m, pair);
                    if (totalMin <= 60) IncrementDeal(deals1hr, pair);
                    //if (totalMin <= 120) IncrementDeal(deals2hr, pair);
                    if (totalMin <= 240) IncrementDeal(deals4hr, pair);
                    if (totalMin > 240) break;
                }

                foreach (Deal deal in deals.Data)
                {
                    DateTime ClosedAt = (DateTime)deal.ClosedAt;
                    double minutes = Math.Round(ClosedAt.Subtract(deal.CreatedAt).TotalMinutes, 1);
                    double ago = Math.Round(DateTime.UtcNow.Subtract(ClosedAt).TotalMinutes, 1);
                    decimal profit = Decimal.Round((decimal)deal.FinalProfitPercentage, 2);
                    if (minutes <= 10 && minutes > 0 && profit > 0) //don't show if it took longer than 15 minutes
                    {
                        // ({deal.BotName})
                        string pair = deal.Pair.Replace("USDT_", "");

                        //doesn't make sense for it not to exist really unless Data is dynamic and the last run missed it
                        if (!deals30m.ContainsKey(pair)) IncrementDeal(deals30m, pair);
                        if (!deals1hr.ContainsKey(pair)) IncrementDeal(deals1hr, pair);
                        if (!deals4hr.ContainsKey(pair)) IncrementDeal(deals4hr, pair);

                        string output = $@"{pair} - {minutes} minutes, {profit}% profit [Deal Count: 30m - {deals30m[pair]}, 1h - {deals1hr[pair]}, 4h - {deals4hr[pair]}]";//, closed at {ClosedAt.ToShortTimeString()}, {ago} minutes ago";
                        if (!lookup.ContainsKey(deal.Id) && !firstRun)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            if (!hasPlayed)
                            {
                                //PlaySound();
                                //Console.Beep();
                                hasPlayed = true;
                            }
                            //IncreaseDealsForBotsByName(deal.BotName, 1, 5);
                            sb.AppendLine(output);
                        }
                        else Console.ForegroundColor = ConsoleColor.White;
                        if (!lookup.ContainsKey(deal.Id)) lookup.Add(deal.Id, true);
                        Console.WriteLine(output);
                    }
                    if (ago > 30) break; //don't show greater 30 min ago
                }
                if (sb.Length > 0) dw.SendMessage(sb.ToString()).GetAwaiter().GetResult(); //send to discord
                Task.Delay(1 * 60 * 1000).GetAwaiter().GetResult();
                firstRun = false;
            }
            //Console.ReadKey();
        }

        static void IncreaseDealsForBotsByName(string BotName, int numNewDeals, int realMaxDeals)
        {
            var api = new XCommas.Net.XCommasApi(key, secret, default, UserMode.Real); //increase deals for bots with this name in real account
            var bots = api.GetBotsAsync().Result;
            foreach(Bot bot in bots.Data)
            {
                if(bot.Name == BotName && bot.IsEnabled)
                {
                    int maxDeals = bot.MaxActiveDeals;
                    if (numNewDeals + maxDeals > realMaxDeals)
                    {
                        bot.MaxActiveDeals = realMaxDeals;
                    }
                    else bot.MaxActiveDeals = maxDeals + numNewDeals;
                }
            }
        }

        //static void PlaySound()
        //{
        //    SoundPlayer sp = new SoundPlayer();
        //    sp.SoundLocation = Environment.CurrentDirectory + "/sound.wav";
        //    sp.Play();
        //}
    }

    class DiscordWrapper
    {
        string _token, _msg;
        DiscordSocketClient _client;
        public DiscordWrapper(string token) { _token = token; }
        public async Task SendMessage(string msg)
        {
            _msg = msg;
            _client = new DiscordSocketClient();
            await _client.LoginAsync(TokenType.Bot, _token);
            await _client.StartAsync();
            _client.Ready += _client_Ready;
            //await Task.Delay(-1);
        }
        private async Task _client_Ready()
        {
            var guild = _client.GetGuild(817400573615800320); // guild id
            if (guild != null)
            {
                var channel = guild.GetTextChannel(899962840532795412); // channel id
                await channel.SendMessageAsync(_msg);
            }
            _client.Dispose();
            //Environment.Exit(0);
        }
    }
}
