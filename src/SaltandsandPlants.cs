using System;
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
    public class BlockSASWaterPlant : BlockPlant
    {
        public ICoreAPI Api => api;
        private int maxDepth;
        private string waterCode;
        private int minDepth;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (Attributes["maxDepth"].Exists & Attributes["minDepth"].Exists & Attributes["waterCode"].Exists)
            {
                minDepth = Attributes["minDepth"].AsInt(2);
                maxDepth = Attributes["maxDepth"].AsInt(6);
                waterCode = Attributes["waterCode"].AsString();
            }

        }

		public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
		      {
		          Block block = blockAccessor.GetBlock(pos.DownCopy());
		          return (block.Fertility > 0) || (blockAccessor.GetBlock(pos).LiquidCode == waterCode);
		      }
		
        // Worldgen placement, tests to see how many blocks below water the plant is being placed, and if that's allowed for the plant
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            Block block = blockAccessor.GetBlock(pos);

            if (!block.IsReplacableBy(this))
            {
                return false;
            }
 
            Block belowBlock = blockAccessor.GetBlock(pos.DownCopy());
 
            if (belowBlock.Fertility > 0 && minDepth == 0)
            {
                Block placingBlock = blockAccessor.GetBlock(Code);
                if (placingBlock == null) return false;
                
                blockAccessor.SetBlock(placingBlock.BlockId, pos);
                return true;
            }

            if(belowBlock.LiquidCode == waterCode)
            {
				if(belowBlock.LiquidCode != waterCode) return false;
                for(var currentDepth = 1; currentDepth <= maxDepth + 1; currentDepth ++)
                {
                    belowBlock = blockAccessor.GetBlock(pos.DownCopy(currentDepth));
                    if (belowBlock.Fertility > 0)
                    {
      Block aboveBlock = blockAccessor.GetBlock(pos.DownCopy(currentDepth - 1));
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
 
    public class BlockSASSeaweed : BlockWaterPlant
    {
        public override string RemapToLiquidsLayer { get { return "saltwater-still-7"; } }

		public ICoreAPI Api => api;
        Random random = new Random();
        Block[] blocks;
		
		private string waterCode;
		private int maxDepth;
        private int minDepth;
		private int minLength; 
		private int maxLength;
		private int minReplaceable;
		private bool useComplexGeneration;		
		private bool placeBase;
		
		public string[] segments = new string[] { "segment1", null, "segment2", null};
        public string[] bases = new string[] { "base1", "base1-short", "base2", "base2-short"};
        public string[] ends = new string[] { "end1", null, "end2", null};

		public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
			
			// Base min/max depth and water code attributes common to most block classes in this library
			if (Attributes["maxDepth"].Exists & Attributes["minDepth"].Exists & Attributes["waterCode"].Exists)
            {
                minDepth = Attributes["minDepth"].AsInt(2);
                maxDepth = Attributes["maxDepth"].AsInt(10);
                waterCode = Attributes["waterCode"].AsString("saltwater");
            }
			
			// Determine what the minimum replaceable value to place a block over
			if (Attributes["minReplaceable"].Exists)
			{
				minReplaceable = Attributes["minReplaceable"].AsInt(3000);
			}
			
			// Determines whether to place a block from the bases array as the first block or just to place a segment as the first block
			if (Attributes["placeBase"].Exists)
			{
				placeBase = Attributes["placeBase"].AsBool(false);
			}
			
			// Determines whether to use the complex generation method or not, the simple generation method uses only one set of block codes,
			// namely either "segment1", "end1" and "base1" if placeBase is true, or "segment", "end", and "base" if the numbered codes don't exist
			// generation will fail entirely if neither of these sets of block variant codes exist
			if (Attributes["useComplexGeneration"].Exists)
			{
				useComplexGeneration = Attributes["useComplexGeneration"].AsBool(false);
			}			
			
			// Determines the minimum and maximum length of the seaweed column, if the minLength is defined but maxLength isn't
			// maxLength will be set to double minLength
			if (Attributes["minLength"].Exists)
			{
				minLength = Attributes["minLength"].AsInt(3);
				if (Attributes["maxLength"].Exists)
				{
					maxLength = Attributes["maxLength"].AsInt(6);
				}
				else
				{
					// Just assume it's minlength * 2, the default length is random.Next(3) + random.Next(3)
					maxLength = minLength * 2;
				}
			}
			else
			{
				minLength = 3;
				maxLength = 6;
			}
        }
		
        public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.DownCopy());
            return (block.Fertility > 0) || (block is BlockSASSeaweed) || (blockAccessor.GetBlock(pos).LiquidCode == waterCode);
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            int windData =
                ((api.World.BlockAccessor.GetBlock(pos.DownCopy(1)) is BlockSASSeaweed) ? 1 : 0)
                + ((api.World.BlockAccessor.GetBlock(pos.DownCopy(2)) is BlockSASSeaweed) ? 1 : 0)
                + ((api.World.BlockAccessor.GetBlock(pos.DownCopy(3)) is BlockSASSeaweed) ? 1 : 0)
            ;

            for (int i = 0; i < sourceMesh.FlagsCount; i++)
            {
                float y = sourceMesh.xyz[i * 3 + 1];
                sourceMesh.Flags[i] = (sourceMesh.Flags[i] & VertexFlags.ClearWindDataBitsMask) | (windData + (y > 0 ? 1 : 0)) << VertexFlags.WindDataBitsPos;
            }
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            BlockPos belowPos = pos.DownCopy();
			bool completed = false;
            Block block = blockAccessor.GetBlock(belowPos, BlockLayersAccess.Fluid);
            if (block.LiquidCode != waterCode) return false;

            int depth = 1;
            while (depth < maxDepth)
            {
                belowPos.Down();
                block = blockAccessor.GetBlock(belowPos);

                if (block.Fertility > 0)
                {
                    belowPos.Up();
					if (!useComplexGeneration)
					{
						if ( PlaceSeaweed(blockAccessor, belowPos, depth, worldGenRand) == true ) completed = true; 
					} else {
						if ( PlaceSeaweedComplex(blockAccessor, belowPos, depth, worldGenRand) == true ) completed = true;
					}
                } else
                {
                    if (!block.IsLiquid()) return false;
                }

                depth++;
            }

            return completed;

        }

		/// <summary>
        /// Generates a column of blocks upwards only using the first element in the
        /// </summary>
        private bool PlaceSeaweed(IBlockAccessor blockAccessor, BlockPos pos, int depth, IRandom worldGenRand)
        {
            int height = Math.Min(depth-1,  minLength + random.Next(1+(maxLength-minLength)));

            if (blocks == null)
            {
				// Check whether the block has numbered variants or not, use the numbered variants if they exist
				if ( blockAccessor.GetBlock(CodeWithParts("segment1")) != null )
				{
					blocks = new Block[]
					{
						blockAccessor.GetBlock(CodeWithParts("segment1")),
						blockAccessor.GetBlock(CodeWithParts("end1")),
					};
				}
				else if ( blockAccessor.GetBlock(CodeWithParts("segment")) != null )
				{
					blocks = new Block[]
					{
						blockAccessor.GetBlock(CodeWithParts("segment1")),
						blockAccessor.GetBlock(CodeWithParts("end1")),
					};
				}
				else
				{
					Api.World.Logger.Error("Block ({0}) with class BlockSASSeaweed had no appropriate variants for 'segment1' or 'segment'");
					return false;
				}
                
            }
			// Place a base block and begin segments above it, otherwise begin segments immediately
			if (placeBase == true)
			{
				blockAccessor.SetBlock(blockAccessor.GetBlock(CodeWithParts("base1")).BlockId,pos);
				pos.Up();
				height--;
            }
			// Place segments upwards in a column until height is 0 then terminate in an end block
			while (height-- > 0)
            {
                blockAccessor.SetBlock(height == 0 ? blocks[1].BlockId : blocks[0].BlockId, pos);
                pos.Up();
            }
			return true;
        }
		
		/// <summary>
        /// Generates a column of blocks upwards choosing a number (rnd) from 0 to bases.Length, placing a base block from bases[rnd] and only a base if segments[rnd] is null
		/// If segments[rnd] is not null (it could be if only the base or a "short" base is to be placed) it will place a length of blocks of segments[rnd] topped by a block of ends[rnd]
		/// The length of segments is a random number based on placeMaxLength + rand.NextInt(placeMaxLengthRand)
        /// </summary>
		private bool PlaceSeaweedComplex(IBlockAccessor blockAccessor, BlockPos pos, int depth, IRandom worldGenRand)
        {
            int rnd = worldGenRand.NextInt(bases.Length);

			//Block placeblock = api.World.GetBlock(CodeWithVariant("type", bases[rnd]));
			//blockAccessor.SetBlock(placeblock.Id, pos);
			
			// Attempt to place a base block and check if the appropriate code exists
            if (placeBase == true)
            {
				Block baseblock = blockAccessor.GetBlock(CodeWithVariant("type", bases[rnd]));
				if (baseblock != null)
				{
					blockAccessor.SetBlock(baseblock.Id, pos);
					pos.Up();
				}
				else
				{
					Api.World.Logger.Error("Block with class BlockSASSeaweed had no base block for random value {1} (it would be {2})");
					return false;
				}
			}
			
            if (segments[rnd] != null)
            {
                //placeblock = api.World.GetBlock(CodeWithVariant("type", segments[rnd]));

				if (blocks == null)
				{
					blocks = new Block[]
					{
						blockAccessor.GetBlock(CodeWithVariant("type", segments[rnd])),
						blockAccessor.GetBlock(CodeWithVariant("type", ends[rnd])),
					};
				}
				//int len = placeMaxLength + worldGenRand.NextInt(placeMaxLengthRand+1);
                int len = Math.Min(placeBase == true ? depth-1 : depth, minLength+worldGenRand.NextInt(maxLength-minLength) + (placeBase == true ? 0 : 1));
                while (len-- > 0)
                {
                    if (blockAccessor.GetBlock(pos).Replaceable > minReplaceable)
                    {
						blockAccessor.SetBlock(len == 0 ? blocks[1].BlockId : blocks[0].BlockId, pos);
                        //blockAccessor.SetBlock(placeblock.Id, pos);
                    }
					pos.Up();
                }
				
				
                //pos.Up();
                //placeblock = api.World.GetBlock(CodeWithVariant("type", ends[rnd]));
                //if (blockAccessor.GetBlock(pos).Replaceable > minReplaceable)
                //{
                //    blockAccessor.SetBlock(placeblock.Id, pos);
                //}
				
				
				
            }
			return true;
        }
		
    }
	
}
