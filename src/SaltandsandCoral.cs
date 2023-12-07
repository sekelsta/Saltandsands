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
    public class BlockSASCoralSeed : Block
    {
        public ICoreAPI Api => api;
        private int maxDepth;
        private string waterCode;
        private int minDepth;
		private int reefRadiusMin;
		private int reefRadiusMax;
		private int reefMaxHeightBelowSealevel;
		private int reefMaxHeightAboveSealevel;
		private int maxSlopeHeightDiff;
		public string[] coralStrings = new string[] { 
		"coralbrain-blue", "coralbrain-green", "coralbrain-red", "coralbrain-yellow",
		"coralfan-blue", "coralfan-orange", "coralfan-purple", "coralfan-red", "coralfan-violet", "coralfan-yellow", 
		"coralstaghorn-blue", "coralstaghorn-orange", "coralstaghorn-purple", "coralstaghorn-yellow", 
		"coraltable-brown", "coraltable-gray", "coraltable-green", "coraltable-red", 
		"coraltube-blue", "coraltube-orange", "coraltube-pink", "coraltube-purple", "coraltube-red", "coraltube-yellow" };
		private AssetLocation[] coralTypes = new AssetLocation[24];
		private AssetLocation coralSubstrateBlock = new AssetLocation;
		private bool extremeSlopeCancels;
		private int extremeSlopeThreshold;
		BlockPos tmpPos = new BlockPos();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
			
			// Typical base characteristics of ocean generating blocks- minimum depth, maximum depth and water type/liquid code to generate in
			if (Attributes["maxDepth"].Exists & Attributes["minDepth"].Exists & Attributes["waterCode"].Exists) 
			{
				minDepth = Attributes["minDepth"].AsInt(2);
				maxDepth = Attributes["maxDepth"].AsInt(6);
				waterCode = Attributes["waterCode"].AsString("saltwater");
			}
			
			// Define the minimum and maximum radius values for a coral reef
			if (Attributes["reefRadiusMin"].Exists & Attributes["reefRadiusMax"].Exists)
			{
				reefRadiusMin = Attributes["reefRadiusMin"].AsInt(2);
				reefRadiusMax = Attributes["reefRadiusMax"].AsInt(12);
			}
			else
			{
				reefRadiusMin = 2;
				reefRadiusMax = 12;			
			}
			
			if (Attributes["extremeSlopeCancels"].Exists)
			{
				extremeSlopeCancels = Attributes["extremeSlopeCancels"].AsBool(false);
			}
			else
			{
				extremeSlopeCancels = false;
			}
			
			if (Attributes["extremeSlopeThreshold"].Exists)
			{
				extremeSlopeThreshold = Attributes["extremeSlopeThreshold"].AsInt(5);
			}
			else
			{
				extremeSlopeThreshold = 5;
			}
			
			reefMaxHeightBelowSealevel = Attributes["reefMaxHeightBelowSealevel"].Exists ? Attributes["reefMaxHeightBelowSealevel"].AsInt(15) : 15;
			reefMaxHeightAboveSealevel = Attributes["reefMaxHeightAboveSealevel"].Exists ? Attributes["reefMaxHeightAboveSealevel"].AsInt(5) : 5;
			//maxSlopeHeightDiff = Attributes["maxSlopeHeightDiff"].Exists ? Attributes["maxSlopeHeightDiff"].AsInt(5) : 5;
			// Define maximum height difference between the corners of a potential reef above which generation is cancelle
			if (Attributes["maxSlopeHeightDiff"].Exists)
			{
				maxSlopeHeightDiff = Attributes["maxSlopeHeightDiff"].AsInt(5);
			}
			else
			{
				maxSlopeHeightDiff = 5;
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
			
			if (Attributes["coralSubstrateBlock"].Exists) 
			{
				coralSubstrateBlock = new AssetLocation("saltandsands:" + Attributes["coralSubstrateBlock"].AsString("coralsubstrate"));
			}
			else
			{
				coralSubstrateBlock = new AssetLocation("saltandsands:coralsubstrate");
			}
			
			//coralTypes = this.Attributes["coralTypes"].AsString();
			//var assetCodes = Block.Attributes["suitableFor"].Token.ToObject<IEnumerable<string>>();

        }

        // Worldgen placement, tests to see how many blocks below water the plant is being placed, and if that's allowed for the plant
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
				
				int cnt = reefRadiusMin + worldgenRand.NextInt(reefRadiusMax - reefRadiusMin);
				//float depth = GameMath.Sqrt(GameMath.Sqrt(cnt));
				float reefRadius = GameMath.Sqrt(cnt) * 1.25f;

				// Check whether the reef would be spawning above sea level for some reason 
				if (pos.Y > api.World.SeaLevel + reefMaxHeightAboveSealevel || pos.Y < reefMaxHeightBelowSealevel) return false;
				
				// Perform a check to see if the area has extreme slopes
				if (extremeSlopeCancels)
				{
					// Check the four corners of a square for extreme height differences
					int y1 = blAcc.GetTerrainMapheightAt(tmpPos.Set(pos.X - extremeSlopeThreshold, pos.Y, pos.Z));
					int y2 = blAcc.GetTerrainMapheightAt(tmpPos.Set(pos.X + extremeSlopeThreshold, pos.Y, pos.Z));
					int y3 = blAcc.GetTerrainMapheightAt(tmpPos.Set(pos.X, pos.Y, pos.Z + extremeSlopeThreshold));
					int y4 = blAcc.GetTerrainMapheightAt(tmpPos.Set(pos.X, pos.Y, pos.Z - extremeSlopeThreshold));

					if ((GameMath.Max(y1, y2, y3, y4) - GameMath.Min(y1, y2, y3, y4)) > maxSlopeHeightDiff)
					{
						Api.World.Logger.Error("Coral reef failed to place due to extreme slope! Difference {0} > threshold {1} ",GameMath.Max(y1, y2, y3, y4) - GameMath.Min(y1, y2, y3, y4),maxSlopeHeightDiff);
						return false;
					}
				}
				
			tmpPos = tmpPos.Set(pos.X, pos.Y, pos.Z);	
            float reefSubstrateRadius = reefRadius * 1.2f;
            int range = (int)Math.Ceiling(impactRockRadius);
            int chunksize = api.World.BlockAccessor.ChunkSize;
            Vec2i vecTmp = new Vec2i();

			for (int dx = -range; dx <= range; dx++)
            {
                for (int dz = -range; dz <= range; dz++)
                {
					float distReefSubstrateEdge = (dx * dx + dz * dz) / (reefSubstrateRadius * reefSubstrateRadius);
                    if (distReefSubstrateEdge > 1) continue;
					
					tmpPos.X = pos.X + dx;
                    tmpPos.Z = pos.Z + dz;
					tmpPos.Y = blockAccessor.GetTerrainMapheightAt(tmpPos);

					Block block = blockAccessor.GetBlock(tmpPos);
                    blockAccessor.SetBlock(coralSubstrateBlock, tmpPos);
					int rnd = worldGenRand.NextInt(coralTypes.Length-1);
					Api.World.Logger.Error("Selected coral type {0}",coralTypes[rnd]);				
					Block coralPlacingBlock = blockAccessor.GetBlock(coralTypes[rnd]);
					Api.World.Logger.Error("Coral block resolved: {0}, block ID: {1}",coralPlacingBlock.Code,coralPlacingBlock.Id);
					blockAccessor.SetBlock(coralPlacingBlock.Id, tmpPos.UpCopy());
					Api.World.Logger.Error("Placed coral substrate at depth {0}, coral type: {1}, coral code: {2}!",currentDepth,coralStrings[rnd],coralTypes[rnd]);
                    
				}
			}
			
			return true;
				
        }   
		
		private bool IsSolid(IBlockAccessor blAcc, int x, int y, int z)
        {
            return blAcc.IsSideSolid(x, y, z, BlockFacing.UP);
        }
    }
	
}