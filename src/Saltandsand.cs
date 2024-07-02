using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Saltandsands
{
     public class Core : ModSystem
    { 
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterItemClass("Bivalve",typeof(ItemBivalve));
            api.RegisterItemClass("Livebivalve",typeof(ItemLiveBivalve));
            api.RegisterBlockClass("BlockBivalve", typeof(BlockBivalve));
            //api.RegisterBlockClass("BlockSASSeaweed", typeof(BlockSASSeaweed));
            api.RegisterBlockClass("BlockSASWaterPlant", typeof(BlockSASWaterPlant));
            //api.RegisterBlockClass("BlockSASCoralSeed", typeof(BlockSASCoralSubstrate));
        }
    }
}
