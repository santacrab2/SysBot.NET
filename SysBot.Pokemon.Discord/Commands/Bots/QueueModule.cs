using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    [EnabledInDm(false)]
    [DefaultMemberPermissions(GuildPermission.ViewChannel)]
    public class QueueModule : InteractionModuleBase<SocketInteractionContext> 
    {
        public static TradeQueueInfo<PB8> Info => SysCord<PB8>.Runner.Hub.Queues.Info;

        [SlashCommand("queuestatus", "Checks the user's position in the queue.")]
    
        public async Task GetTradePositionAsync()
        {
            await DeferAsync();
            var msg = Context.User.Mention + " - " + Info.GetPositionString(Context.User.Id);
            await FollowupAsync(msg,ephemeral:true).ConfigureAwait(false);
        }

        [SlashCommand("queueclear", "Clears the user from the trade queues. Will not remove a user if they are being processed.")]
      
        public async Task ClearTradeAsync()
        {
            await DeferAsync();
            string msg = ClearTrade();
            await FollowupAsync(msg,ephemeral:true).ConfigureAwait(false);
        }


        private string ClearTrade()
        {
            var userID = Context.User.Id;
            return ClearTrade(userID);
        }

        //private static string ClearTrade(string username)
        //{
        //    var result = Info.ClearTrade(username);
        //    return GetClearTradeMessage(result);
        //}

        private static string ClearTrade(ulong userID)
        {
            var result = Info.ClearTrade(userID);
            return GetClearTradeMessage(result);
        }

        private static string GetClearTradeMessage(QueueResultRemove result)
        {
            return result switch
            {
                QueueResultRemove.CurrentlyProcessing => "Looks like you're currently being processed! Did not remove from all queues.",
                QueueResultRemove.CurrentlyProcessingRemoved => "Looks like you're currently being processed!",
                QueueResultRemove.Removed => "Removed you from the queue.",
                _ => "Sorry, you are not currently in the queue.",
            };
        }
    }
}
