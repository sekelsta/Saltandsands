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
        private int minDepth;
		private AssetLocation[];


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            minDepth = this.Attributes["minDepth"].AsInt(2);
            maxDepth = this.Attributes["maxDepth"].AsInt(6);
			waterCode = this.Attributes["waterCode"].AsString();

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
						
                        Block placingBlock = blockAccessor.GetBlock(Code);
                        if (placingBlock == null) return false;

                        blockAccessor.SetBlock(placingBlock.BlockId, pos.DownCopy(currentDepth - 1));
                        return true;
                    }
                }
            }

            return false;
        }   
    }
}