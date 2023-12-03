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
    public class BlockSASCoralSubstrate : Block
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
			Api.World.Logger.Error("Beginning coral substrate type registration");
			for (var i = 0; i < coralStrings.Length; i++)
			{
                //coralTypes[i] = Api.World.GetBlock(new AssetLocation(Code.Domain + ":" + coralStrings[i]));
                /* You can't use Assetlocation to return a block it won't let you so I changed it too this > */
                coralTypes[i] = new AssetLocation("saltandsands:" + coralStrings[i]);
				Api.World.Logger.Error("Converted coral type {0} to code {1} for assetlocation {2}",coralStrings[i],"saltandsands:"+coralStrings[i],coralTypes[i]);
               
			}
			
			//coralTypes = this.Attributes["coralTypes"].AsString();
			//var assetCodes = Block.Attributes["suitableFor"].Token.ToObject<IEnumerable<string>>();

        }

        // Worldgen placement, tests to see how many blocks below water the plant is being placed, and if that's allowed for the plant
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {

            Block aboveBlock = blockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z);

				int rnd = worldGenRand.NextInt(coralTypes.Length-1);
				Api.World.Logger.Error("Selected coral type {0}",coralTypes[rnd]);				
				Block coralPlacingBlock = blockAccessor.GetBlock(coralTypes[rnd]);
				Api.World.Logger.Error("Coral block resolved: {0}, block ID: {1}",coralPlacingBlock.Code,coralPlacingBlock.Id);
				blockAccessor.SetBlock(coralPlacingBlock.Id, pos.UpCopy());
				Api.World.Logger.Error("Placed coral substrate at depth {0}, coral type: {1}, coral code: {2}!",currentDepth,coralStrings[rnd],coralTypes[rnd]);
				return true;
        }   
    }
	
	public class BlockSASCoral : BlockWaterPlant
    {
		public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            return (block is BlockSASCoralSubstrate) || (blockAccessor.GetBlock(pos.X, pos.Y, pos.Z).LiquidCode == waterCode);
        }
	}
}