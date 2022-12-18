using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord.Net;
using PKHeX.Core;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using PKHeX.Core.AutoMod;
using PKHeX.Drawing.PokeSprite;
using System.Drawing;

namespace SysBot.Pokemon.Discord
{
    public static class QueueHelper<T> where T : PKM, new()
    {
        private const uint MaxTradeCode = 9999_9999;

        public static async Task AddToQueueAsync(SocketInteractionContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader, List<pictocodes>lgcode )
        {
            if ((uint)code > MaxTradeCode)
            {
                await context.Interaction.FollowupAsync("Trade code should be 00000000-99999999!").ConfigureAwait(false);
                return;
            }

            try
            {
                const string helper = "I've added you to the queue! I'll message you here when your trade is starting.";
                IUserMessage test = await trader.SendMessageAsync(helper).ConfigureAwait(false);

                // Try adding
                var result = AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader,lgcode, out var msg);

                // Notify in channel
                await context.Interaction.FollowupAsync(msg).ConfigureAwait(false);
                // Notify in PM to mirror what is said in the channel.
                if (trade is PB7)
                {
                    var (thefile, lgcodeembed) = CreateLGLinkCodeSpriteEmbed(lgcode);
                    await trader.SendFileAsync(thefile, $"{msg}\nYour trade code will be.", embed: lgcodeembed).ConfigureAwait(false);
                }
                else
                {
                    await trader.SendMessageAsync($"{msg}\nYour trade code will be: {code:0000 0000}");
                }

         
            }
            catch (HttpException ex)
            {
                await HandleDiscordExceptionAsync(context, trader, ex).ConfigureAwait(false);
            }
        }

        public static async Task AddToQueueAsync(SocketInteractionContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, List<pictocodes>lgcode)
        {
            await AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User,lgcode).ConfigureAwait(false);
        }

        private static bool AddToTradeQueue(SocketInteractionContext context, T pk, int code, string trainerName, RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader,List<pictocodes>lgcode, out string msg)
        {
            var user = trader;
            var userID = user.Id;
            var name = user.Username;

            var trainer = new PokeTradeTrainerInfo(trainerName, userID);
            var notifier = new DiscordTradeNotifier<T>(pk, trainer, code, user,lgcode);
            var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, lgcode, sig == RequestSignificance.Favored);
            var trade = new TradeEntry<T>(detail, userID, type, name);

            var hub = SysCord<T>.Runner.Hub;
            var Info = hub.Queues.Info;
            var added = Info.AddToTradeQueue(trade, userID, sig == RequestSignificance.Owner);

            if (added == QueueResultAdd.AlreadyInQueue)
            {
                msg = "Sorry, you are already in the queue.";
                return false;
            }

            var position = Info.CheckPosition(userID, type);

            var ticketID = "";
            if (TradeStartModule<T>.IsStartChannel(context.Channel.Id))
                ticketID = $", unique ID: {detail.ID}";

            var pokeName = "";
            if (t == PokeTradeType.Specific && pk.Species != 0)
                pokeName = $" Receiving: {(Species)pk.Species}.";
            msg = $"{user.Mention} - Added to the {type} queue{ticketID}. Current Position: {position.Position}.{pokeName}";

            var botct = Info.Hub.Bots.Count;
            if (position.Position > botct)
            {
                var eta = Info.Hub.Config.Queues.EstimateDelay(position.Position, botct);
                msg += $" Estimated: {eta:F1} minutes.";
            }
            return true;
        }

        private static async Task HandleDiscordExceptionAsync(SocketInteractionContext context, SocketUser trader, HttpException ex)
        {
            string message = string.Empty;
            switch (ex.DiscordCode)
            {
                case DiscordErrorCode.InsufficientPermissions or DiscordErrorCode.MissingPermissions:
                    {
                        // Check if the exception was raised due to missing "Send Messages" or "Manage Messages" permissions. Nag the bot owner if so.
                        var permissions = context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel);
                        if (!permissions.SendMessages)
                        {
                            // Nag the owner in logs.
                            message = "You must grant me \"Send Messages\" permissions!";
                            Base.LogUtil.LogError(message, "QueueHelper");
                            return;
                        }
                        else if (!permissions.ManageMessages)
                        {
                            var app = await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                            var owner = app.Owner.Id;
                            message = $"<@{owner}> You must grant me \"Manage Messages\" permissions!";
                        }
                    }; break;
                case DiscordErrorCode.CannotSendMessageToUser:
                    {
                        // The user either has DMs turned off, or Discord thinks they do.
                        message = context.User == trader ? "You must enable private messages in order to be queued!" : "The mentioned user must enable private messages in order for them to be queued!";
                    }; break;
                default:
                    {
                        // Send a generic error message.
                        message = ex.DiscordCode != null ? $"Discord error {(int)ex.DiscordCode}: {ex.Reason}" : $"Http error {(int)ex.HttpCode}: {ex.Message}";
                    }; break;
            }
            await context.Interaction.FollowupAsync(message).ConfigureAwait(false);
        }
        public static (string,Embed) CreateLGLinkCodeSpriteEmbed(List<pictocodes> lgcode)
        {
            int codecount = 0;
            List<System.Drawing.Image> spritearray = new();
            foreach (pictocodes cd in lgcode)
            {


                var showdown = new ShowdownSet(cd.ToString());
                PKM pk = SaveUtil.GetBlankSAV(EntityContext.Gen7b, "pip").GetLegalFromSet(showdown, out _);
                System.Drawing.Image png = pk.Sprite();
                var destRect = new Rectangle(-40, -65, 137, 130);
                var destImage = new Bitmap(137, 130);

                destImage.SetResolution(png.HorizontalResolution, png.VerticalResolution);

                using (var graphics = Graphics.FromImage(destImage))
                {
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.DrawImage(png, destRect, 0, 0, png.Width, png.Height, GraphicsUnit.Pixel);

                }
                png = destImage;
                spritearray.Add(png);
                codecount++;
            }
            int outputImageWidth = spritearray[0].Width + 20;

            int outputImageHeight = spritearray[0].Height - 65;

            Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (Graphics graphics = Graphics.FromImage(outputImage))
            {
                graphics.DrawImage(spritearray[0], new Rectangle(0, 0, spritearray[0].Width, spritearray[0].Height),
                    new Rectangle(new Point(), spritearray[0].Size), GraphicsUnit.Pixel);
                graphics.DrawImage(spritearray[1], new Rectangle(50, 0, spritearray[1].Width, spritearray[1].Height),
                    new Rectangle(new Point(), spritearray[1].Size), GraphicsUnit.Pixel);
                graphics.DrawImage(spritearray[2], new Rectangle(100, 0, spritearray[2].Width, spritearray[2].Height),
                    new Rectangle(new Point(), spritearray[2].Size), GraphicsUnit.Pixel);
            }
            System.Drawing.Image finalembedpic = outputImage;
            var filename = $"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png";
            finalembedpic.Save(filename);
            filename = System.IO.Path.GetFileName($"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png");
            Embed returnembed = new EmbedBuilder().WithTitle($"{lgcode[0]}, {lgcode[1]}, {lgcode[2]}").WithImageUrl($"attachment://{filename}").Build();
            return (filename,returnembed);
        }
    }
}
