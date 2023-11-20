using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using System.Linq;
using Vintagestory.API.Common.Entities;

namespace Saltandsands
{
    public class BlockSASCoralSubstrate : BlockPlant
    {
        public ICoreAPI Api => api;
        private int maxDepth;
        private string waterCode;
        private int minDepth;
		public string[] coralStrings = new string[] { 
		"coralbrain-blue", "coralbrain-green", "coralbrain-red", "coralbrain-yellow",
		"coralfan-blue", "coralfan-orange", "coralfan-purple", "coralfan-red", "coralfan-violet", "coralfan-yellow", 
		"coralstaghorn-blue", "coralstaghorn-orange", "coralstaghorn-purple", "coralstaghorn-yellow", 
		"coraltable-brown", "coraltable-gray", "coraltable-green", "coraltable-red", 
		"coraltube-blue", "coraltube-orange", "coraltube-pink", "coraltube-purple", "coraltube-red", "coraltube-yellow" };
		private AssetLocation[] coralTypes = new AssetLocation[24];

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
			if (Attributes["maxDepth"].Exists & Attributes["minDepth"].Exists & Attributes["waterCode"].Exists) 
			{
				minDepth = Attributes["minDepth"].AsInt(2);
				maxDepth = Attributes["maxDepth"].AsInt(6);
				waterCode = Attributes["waterCode"].AsString("saltwater");
			}
			// Get asset locations for coral blocks using coralStrings
			for (var i = 0; i <= coralStrings.Length; i++)
			{
                //coralTypes[i] = Api.World.GetBlock(new AssetLocation(Code.Domain + ":" + coralStrings[i]));
                /* You can't use Assetlocation to return a block it won't let you so I changed it too this > */
                coralTypes[i] = new AssetLocation(Code.Domain + ":" + coralStrings[i]);
               
			}
			
			//coralTypes = this.Attributes["coralTypes"].AsString();
			//var assetCodes = Block.Attributes["suitableFor"].Token.ToObject<IEnumerable<string>>();

        }

        // Worldgen placement, tests to see how many blocks below water the plant is being placed, and if that's allowed for the plant
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            Block block = blockAccessor.GetBlock(pos);

            if (!block.IsReplacableBy(this))
            {
                return false;
            }

            Block belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);

			/*
            if (belowBlock.Fertility > 0 && minDepth == 0)
            {
                Block placingBlock = blockAccessor.GetBlock(Code);
                if (placingBlock == null) return false;
                
                blockAccessor.SetBlock(placingBlock.BlockId, pos);
                return true;
            }
			*/
			
            if(belowBlock.LiquidCode == waterCode)
            {
				if(belowBlock.LiquidCode != waterCode) return false;
                for(var currentDepth = 1; currentDepth <= maxDepth + 1; currentDepth ++)
                {
                    belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - currentDepth, pos.Z);
                    if (belowBlock.Fertility > 0)
                    {
						Block aboveBlock = blockAccessor.GetBlock(pos.X, pos.Y - currentDepth + 1, pos.Z);
                        if(aboveBlock.LiquidCode != waterCode) return false;
                        //if(currentDepth < minDepth + 1) return false;
						Api.World.Logger.Notification("Attempting to place coral substrate on lake/seabed, watercode: {0}, depth: {1}",waterCode,currentDepth);
                        Block placingBlock = blockAccessor.GetBlock(Code);
                        if (placingBlock == null) return false;

						if (blockAccessor.GetBlock(pos).Replaceable > 500)
						{
							blockAccessor.SetBlock(placingBlock.BlockId, pos.DownCopy(currentDepth - 1));
						
							int rnd = worldGenRand.NextInt(coralTypes.Length-1);
							Block coralPlacingBlock = api.World.GetBlock(coralTypes[rnd]);
							blockAccessor.SetBlock(coralPlacingBlock.Id, pos.DownCopy(currentDepth));
							Api.World.Logger.Notification($"Placed coral substrate and coral block successfully!, depth: {waterCode}, coral type: {coralStrings[rnd]}, coral code: {coralTypes[rnd]}"); // Not sure what you are trying to do here.
							return true;
						}
						else
						{
							Api.World.Logger.Notification("Could not place coral at depth {0}!",currentDepth);
						}
                        return false;
                    }
                }
            }
            return false;
        }   
    }
}