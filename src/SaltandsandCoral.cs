using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using System.Linq;
using System;
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
		private AssetLocation[] coralTypes = new AssetLocation[] { 
		"saltandsands:coralbrain-blue", "saltandsands:coralbrain-green", "saltandsands:coralbrain-red", "saltandsands:coralbrain-yellow",
		"saltandsands:coralfan-blue", "saltandsands:coralfan-orange", "saltandsands:coralfan-purple", "saltandsands:coralfan-red", "saltandsands:coralfan-violet", "saltandsands:coralfan-yellow", 
		"saltandsands:coralstaghorn-blue", "saltandsands:coralstaghorn-orange", "saltandsands:coralstaghorn-purple", "saltandsands:coralstaghorn-yellow", 
		"saltandsands:coraltable-brown", "saltandsands:coraltable-gray", "saltandsands:coraltable-green", "saltandsands:coraltable-red", 
		"saltandsands:coraltube-blue", "saltandsands:coraltube-orange", "saltandsands:coraltube-pink", "saltandsands:coraltube-purple", "saltandsands:coraltube-red", "saltandsands:coraltube-yellow" };
		private AssetLocation coralSubstrateBlock = new AssetLocation();
		private bool extremeSlopeCancels;
		private int extremeSlopeThreshold;
		BlockPos tmpPos = new BlockPos(0);

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
			// Define maximum height difference between the corners of a potential reef above which generation is cancelle
			if (Attributes["maxSlopeHeightDiff"].Exists)
			{
				maxSlopeHeightDiff = Attributes["maxSlopeHeightDiff"].AsInt(5);
			}
			else
			{
				maxSlopeHeightDiff = 5;
			}
			
			if (Attributes["coralSubstrateBlock"].Exists) 
			{
				coralSubstrateBlock = new AssetLocation("saltandsands:" + Attributes["coralSubstrateBlock"].AsString("coralsubstrate"));
			}
			else
			{
				coralSubstrateBlock = new AssetLocation("saltandsands:coralsubstrate");
			}

        }

        // Worldgen placement, tests to see how many blocks below water the plant is being placed, and if that's allowed for the plant
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
				
				int cnt = reefRadiusMin + worldGenRand.NextInt(reefRadiusMax - reefRadiusMin);
				//float depth = GameMath.Sqrt(GameMath.Sqrt(cnt));
				float reefRadius = GameMath.Sqrt(cnt) * 1.25f;

				// Check whether the reef would be spawning above sea level for some reason 
				if (pos.Y > api.World.SeaLevel + reefMaxHeightAboveSealevel || pos.Y < reefMaxHeightBelowSealevel) return false;
				
				// Perform a check to see if the area has extreme slopes
				if (extremeSlopeCancels)
				{
					// Check the four corners of a square for extreme height differences
					int y1 = blockAccessor.GetTerrainMapheightAt(tmpPos.Set(pos.X - extremeSlopeThreshold, pos.Y, pos.Z));
					int y2 = blockAccessor.GetTerrainMapheightAt(tmpPos.Set(pos.X + extremeSlopeThreshold, pos.Y, pos.Z));
					int y3 = blockAccessor.GetTerrainMapheightAt(tmpPos.Set(pos.X, pos.Y, pos.Z + extremeSlopeThreshold));
					int y4 = blockAccessor.GetTerrainMapheightAt(tmpPos.Set(pos.X, pos.Y, pos.Z - extremeSlopeThreshold));

					if ((GameMath.Max(y1, y2, y3, y4) - GameMath.Min(y1, y2, y3, y4)) > maxSlopeHeightDiff)
					{
						//Api.World.Logger.Error("Coral reef failed to place due to extreme slope! Difference {0} > threshold {1} ",GameMath.Max(y1, y2, y3, y4) - GameMath.Min(y1, y2, y3, y4),maxSlopeHeightDiff);
						return false;
					}
				}
				
			tmpPos = tmpPos.Set(pos.X, pos.Y, pos.Z);	
            float reefSubstrateRadius = reefRadius * 1.2f;
            int range = (int)Math.Ceiling(reefSubstrateRadius);
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
					// a second check to make sure the coral or coral substrate isn't being placed at or above the waterline
					// had some issues with corals invading the land again
					if (blockAccessor.GetBlock(tmpPos).LiquidCode != waterCode || blockAccessor.GetBlock(tmpPos.UpCopy()).LiquidCode != waterCode)
					{
						continue;
					}
                    blockAccessor.SetBlock(api.World.GetBlock(coralSubstrateBlock).BlockId, tmpPos);
					int rnd = worldGenRand.NextInt(coralTypes.Length-1);
					Block coralPlacingBlock = blockAccessor.GetBlock(coralTypes[rnd]);
					blockAccessor.SetBlock(coralPlacingBlock.Id, tmpPos.UpCopy());
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
