using Discord.Interactions;
using Discord;
using System.Threading.Tasks;
using PKHeX.Core;

namespace SysBot.Pokemon.Discord
{
    public class LegalizerModule : InteractionModuleBase<SocketInteractionContext> 
    {
        [SlashCommand("legalize", "Tries to legalize the attached pkm data.")]
       
        public async Task LegalizeAsync(Attachment pkfile)
        {
            await DeferAsync();
           
            await Context.ReplyWithLegalizedSetAsync(pkfile).ConfigureAwait(false);
        }

        [SlashCommand("convert", "Tries to convert the Showdown Set to pkm data.")]

        public async Task ConvertShowdown([Summary("PokemonText")]string content, int gen=0)
        {
            await DeferAsync();
            if (gen == 0)
            {
                await Context.ReplyWithLegalizedSetAsync<PB8>(content).ConfigureAwait(false);return;
            }
            await Context.ReplyWithLegalizedSetAsync(content, gen).ConfigureAwait(false);
        }


    }
}