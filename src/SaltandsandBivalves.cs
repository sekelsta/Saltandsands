﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Saltandsands
{
    public class BlockBivalve : BlockPlant
    {
        public ICoreAPI Api => api;
        private int maxDepth;
        private string waterCode;
        private int minDepth;
		WorldInteraction[] interactions = null;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (Attributes["maxDepth"].Exists & Attributes["minDepth"].Exists & Attributes["waterCode"].Exists)
            {
                minDepth = Attributes["minDepth"].AsInt(2);
                maxDepth = Attributes["maxDepth"].AsInt(6);
                waterCode = Attributes["waterCode"].AsString();
            }
    
			
			//string hab = Variant["habitat"];
            //if (hab == "water") habitatBlockCode = "water-still-7";
            //else if (hab == "ice") habitatBlockCode = "lakeice";

            if (Variant["state"] == "harvested" || Variant["state"] == "establishing") return;

            interactions = ObjectCacheUtil.GetOrCreate(api, "bivalveBlockInteractions", () =>
            {
                List<ItemStack> knifeStacklist = new List<ItemStack>();

                foreach (Item item in api.World.Items)
                {
                    if (item.Code == null) continue;

                    if (item.Tool == EnumTool.Knife)
                    {
                        knifeStacklist.Add(new ItemStack(item));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bivalve-harvest",
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = knifeStacklist.ToArray()
                    }
                };
            });
			
        }

        // Worldgen placement, tests to see how many blocks below water the plant is being placed, and if that's allowed for the plant
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            Block block = blockAccessor.GetBlock(pos);

            if (!block.IsReplacableBy(this))
            {
                return false;
            }

            Block belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);

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
                for(var currentDepth = 0; currentDepth <= maxDepth + 1; currentDepth ++)
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

		 public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (Variant["state"] == "harvested" || Variant["state"] == "establishing") dt /= 2;
            else if (player.InventoryManager.ActiveTool != EnumTool.Knife)
            {
                dt /= 3;
            }
            else
            {
                if (itemslot.Itemstack.Collectible.MiningSpeed.TryGetValue(EnumBlockMaterial.Plant, out float mul)) dt *= mul;
            }

            float resistance = RequiredMiningTier == 0 ? remainingResistance - dt : remainingResistance;

            if (counter % 5 == 0 || resistance <= 0)
            {
                double posx = blockSel.Position.X + blockSel.HitPosition.X;
                double posy = blockSel.Position.Y + blockSel.HitPosition.Y;
                double posz = blockSel.Position.Z + blockSel.HitPosition.Z;
                player.Entity.World.PlaySoundAt(resistance > 0 ? Sounds.GetHitSound(player) : Sounds.GetBreakSound(player), posx, posy, posz, player, true, 16, 1);
            }

            return resistance;
        }
		
		public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                foreach (var bdrop in Drops)
                {
                    ItemStack drop = bdrop.GetNextItemStack();
                    if (drop != null)
                    {
                        world.SpawnItemEntity(drop, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                    }
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
            }

            if (byPlayer != null && Variant["state"] == "normal" && (byPlayer.InventoryManager.ActiveTool == EnumTool.Knife || byPlayer.InventoryManager.ActiveTool == EnumTool.Sickle || byPlayer.InventoryManager.ActiveTool == EnumTool.Scythe))
            {
                world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariants(new string[] { "state" }, new string[] { "harvested" })).BlockId, pos);
                return;
            }

            SpawnBlockBrokenParticles(pos);
            world.BlockAccessor.SetBlock(0, pos);
        }
		
		public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
		
    }

    public class ItemBivalve : Item
    {
        float curX;
        float curY;
        
        float prevSecUsed;
		float processingSecRequired;
        LCGRandom rnd;

        //ItemStack[] processingResultStacks;
        //ItemStack[] rareProcessingResultStacks;
        //double[] rareProcessingResultChances;
		//bool[] isRareResultExclusive;
        //private object pstack; not being used

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            rnd = new LCGRandom(api.World.Seed);
			
			/*
            api.Logger.Error("Resolving processing results...");
            JsonItemStack[] pstacks = Attributes["processingResults"].AsObject<JsonItemStack[]>();
            List<ItemStack> stacklist = new List<ItemStack>();
            //JsonItemStack hhh = jstacks[0];
            //hhh.
            api.Logger.Error("{0} processing results to resolve...",pstacks.Length);
            if (pstacks.Length > 1)
            {
            for (int i = 0; i < pstacks.Length; i++)
            {
                JsonItemStack jstack = pstacks[i];
                jstack.Resolve(api.World, "Bivalve opening result");
                if (jstack.ResolvedItemstack != null)
                {
                    stacklist.Add(jstack.ResolvedItemstack);
                    api.Logger.Error("Resolved processing result #{0}: {1}", i+1 , jstack.ResolvedItemstack.GetName());
                } else
                {
                    api.Logger.Error("Failed to resolve processing result #{0}: {1}!", i+1 , pstacks[i].Code.ToString());
                }
            }
            api.Logger.Error("Completed processing results!");
            processingResultStacks = stacklist.ToArray();
            api.Logger.Error("Processing results added to array");
            stacklist.Clear();
            api.Logger.Error("Stacklist cleared!");
            processingSecRequired = Attributes["processingTime"].AsFloat(1.2f);
            api.Logger.Error("Processing time of {0} registered",processingSecRequired);
            }
            else
            {
                api.Logger.Error("No processing results, is item configured properly?");
            }
            api.Logger.Error("Resolving rare processing results...");
            JsonItemStack[] rstacks = Attributes["rareProcessingResultStacks"].AsObject<JsonItemStack[]>();
            api.Logger.Error("rstacks defined, length: {0}",rstacks.Length);
            
            if (rstacks.Length > 0)
            {
                rareProcessingResultChances = Attributes["rareProcessingResultChances"].AsObject<Double[]>();
                api.Logger.Error("rareProcessingResultChances defined, length: {0}",rareProcessingResultChances.Length);

                List<float> chancelist = new List<float>();  
                api.Logger.Error("{0} rare processing results to resolve...",rstacks.Length);
                for (int i = 0; i < rstacks.Length; i++)
                {
                    JsonItemStack jstack = rstacks[i];
                    jstack.Resolve(api.World, "Bivalve rare opening result");
                    if (jstack.ResolvedItemstack != null)
                    {
                        stacklist.Add(jstack.ResolvedItemstack);
                        api.Logger.Error("Resolved rare processing result #{0}: {1}, drop chance: {2}",i+1,jstack.ResolvedItemstack.GetName(),rareProcessingResultChances[i]);
                    }
                }
                api.Logger.Error("Completed rare processing results!");
                rareProcessingResultStacks = stacklist.ToArray();
                api.Logger.Error("Rare processing results!");

                bool[] resultXRaw = Attributes["rareProcessingResultExclusive"].AsObject<bool[]>();
                
                //bool[] resultX = Attributes["rareProcessingResultExclusive"].AsObject<bool[]>();
                // Check to see if isRareResultExclusive is the same length as the rareProccessingResults
                if (resultXRaw.Length != rareProcessingResultStacks.Length)
                {
                    bool[] resultX = new bool[rareProcessingResultStacks.Length];
                    for (int i = 0; i < rstacks.Length; i++)
                    {
                        if (i > resultXRaw.Length)
                        {
                            resultX[i] = true;   
                        }
                        else
                        {
                            resultX[i] = resultXRaw[i];
                        }
                    }
                    isRareResultExclusive = resultX;
                }
                else
                {
                    isRareResultExclusive = Attributes["rareProcessingResultExclusive"].AsObject<bool[]>();
                }

            }
            else
            {
                //rareProcessingResultStacks = new ItemStack[0];
                //rareProcessingResultChances = new Double[0];
                api.Logger.Error("rareProcessingResults length is zero- no rareProcessingResults to resolve!");
            }
            api.Logger.Error("Done resolving ItemBivalve processing results!");
			*/
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            //if (slot.Itemstack.TempAttributes.GetBool("consumed") == true) return;

            handling = EnumHandHandling.PreventDefault;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            //byEntity.World.RegisterCallback((dt) =>
            //{
                //if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                //{
                    // Todo: Get some audio for shucking the scallops/clams/mussels
                    //byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/messycraft"), byPlayer, byPlayer);
                //}
            //}, 250);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float nowx = 0, nowy = 0;

                if (secondsUsed > 0.3f)
                {
                    int cnt = (int)(secondsUsed * 10);
                    rnd.InitPositionSeed(cnt, 0);

                    float targetx = 3f * (rnd.NextFloat() - 0.5f);
                    float targety = 1.5f * (rnd.NextFloat() - 0.5f);

                    float dt = secondsUsed - prevSecUsed;

                    nowx = (curX - targetx) * dt * 2;
                    nowy = (curY - targety) * dt * 2;
                }

                tf.Translation.Set(nowx - Math.Min(1.5f, secondsUsed*4), nowy, 0);
                //byEntity.Controls.UsingHeldItemTransformBefore = tf;
				byEntity.Controls.UsingHeldItemTransformAfter = tf;

                curX = nowx;
                curY = nowy;

                prevSecUsed = secondsUsed;
				return secondsUsed < processingSecRequired + 0.1f;
            }
            return true;  
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed > processingSecRequired  + 0.1f)
            {
                if (api.Side == EnumAppSide.Server)
                {
                    //ItemStack resultstack = processingResultStacks[api.World.Rand.Next(processingResultStacks.Length)];
                    
					//api.Logger.Error("Finished processing getting results...");
					JsonItemStack[] pstacks = Attributes["processingResults"].AsObject<JsonItemStack[]>();
					//List<ItemStack> stacklist = new List<ItemStack>();
					//api.Logger.Error("{0} processing results to resolve...",pstacks.Length);
					if (pstacks.Length > 1)
					{
						for (int i = 0; i < pstacks.Length; i++)
						{
							JsonItemStack dstack = pstacks[i];
							dstack.Resolve(api.World, "Bivalve opening result");
							ItemStack istack = dstack.ResolvedItemstack;
							if (istack != null)
							{
								//api.Logger.Error("Resolved processing result #{0}: {1}", i+1 , dstack.ResolvedItemstack.GetName());
								//api.World.Logger.Error("Giving resultstack {0} ({1}) x {2} to entity {3}!", istack.GetName(), dstack.Code, dstack.StackSize, byEntity.EntityId);
								if (!byEntity.TryGiveItemStack(istack))
								{
									//api.World.Logger.Error("Entity had insufficient space, dumping item on the ground!");
									byEntity.World.SpawnItemEntity(istack, byEntity.Pos.XYZ.Add(0, 0.5, 0));
								}
								
							} else
							{
								api.Logger.Error("Failed to resolve processing result #{0}!", i+1);
							}
						}
						//api.Logger.Error("Completed processing results!");
						//processingResultStacks = stacklist.ToArray();
						//api.Logger.Error("Processing results added to array");
						//stacklist.Clear();
						//api.Logger.Error("Stacklist cleared!");
					}
					else
					{
						api.Logger.Error("No processing results, is item configured properly?");
					}
					
					
					/*
                    // Handle processing results - shells, meat, other mundane items
                    ItemStack resultstack = ;
                    byEntity.World.Logger.Error("Providing processing results...");
                    for (int i = 0; i < processingResultStacks.Length; i++)
                    {
                        resultstack = processingResultStacks[i].Clone();
                        byEntity.World.Logger.Error("Giving resultstack {0} to entity {1}!", resultstack.GetName(), byEntity.EntityId);
                        if (!byEntity.TryGiveItemStack(resultstack))
                        {
                            byEntity.World.Logger.Error("Entity had insufficient space, dumping item on the ground!");
                            byEntity.World.SpawnItemEntity(resultstack, byEntity.Pos.XYZ.Add(0, 0.5, 0));
                        }
                    }
					*/
					Random ran = new Random();
					JsonItemStack[] rstacks = Attributes["rareProcessingResultStacks"].AsObject<JsonItemStack[]>();
					double[] rchances = Attributes["rareProcessingResultChances"].AsObject<Double[]>();
					if (rstacks.Length > 0)
					{
						api.Logger.Error("rareProcessingResultChances defined, length: {0}",rchances.Length);
 
						api.Logger.Error("{0} rare processing results to resolve...",rstacks.Length);
						for (int i = 0; i < rstacks.Length; i++)
						{
						
							JsonItemStack rdstack = rstacks[i];
							rdstack.Resolve(api.World, "Bivalve rare opening result");
							ItemStack ristack = rdstack.ResolvedItemstack;
							if (ristack != null)
							{
								api.Logger.Error("Resolved processing result #{0}: {1}", i+1 , ristack.GetName());
								
								double roll = byEntity.World.Rand.NextDouble();
								byEntity.World.Logger.Error("Checking rare processing stack {0}, with drop chance of {1} vs roll of {2}!",ristack.GetName(),rchances[i],roll);
								if (roll > rchances[i])
								{
									byEntity.World.Logger.Error("Roll {0} > drop chance {1}, continuing...",roll,rchances[i]);
									continue;
								}
								
								api.World.Logger.Error("Giving resultstack {0} ({1}) x {2} to entity {3}!", ristack.GetName(), rdstack.Code, rdstack.StackSize, byEntity.EntityId);
								if (!byEntity.TryGiveItemStack(ristack))
								{
									api.World.Logger.Error("Entity had insufficient space, dumping item on the ground!");
									byEntity.World.SpawnItemEntity(ristack, byEntity.Pos.XYZ.Add(0, 0.5, 0));
								}
								
							} else
							{
								api.Logger.Error("Failed to resolve processing result #{0}!", i+1);
							}
						}
					}
					
					/*
                    //rareProcessingResultStacks
                    //rareProcessingResultChances
                   
					
                    ItemStack[] rareStacks = rareProcessingResultStacks.Shuffle(ran);
                    double[] rareStackChances = rareProcessingResultChances.Shuffle(ran);
					bool[] isRareExclusive = isRareResultExclusive.Shuffle(ran);
                    byEntity.World.Logger.Error("Rolling rare processing results, rareStacks shuffled...");
                    for (int i = 0; i < rareStacks.Length; i++)
                    {
                        double roll = byEntity.World.Rand.NextDouble();
                        byEntity.World.Logger.Error("Checking rare processing stack {0}, with drop chance of {1} vs roll of {2}!",rareStacks[i].GetName(),rareStackChances[i],roll);
                        if (roll > rareStackChances[i])
                        {
                            byEntity.World.Logger.Error("Roll {0} > drop chance {1}, continuing...",roll,rareStackChances[i]);
                            continue;
                        }
                        resultstack = rareStacks[i].Clone();;
                        byEntity.World.Logger.Error("Roll succeeded, giving resultstack {0} to entity {1}!", resultstack.GetName(), byEntity.EntityId);
                        if (!byEntity.TryGiveItemStack(resultstack))
                        {
                            byEntity.World.Logger.Error("Entity had insufficient space, dumping item on the ground!");
                            byEntity.World.SpawnItemEntity(resultstack, byEntity.Pos.XYZ.Add(0, 0.5, 0));
                        }
						byEntity.World.Logger.Error("Rare drop exclusivity for drop {0} with chance {1} is {2}",rareStacks[i].GetName(),rareStackChances[i],isRareExclusive[i]);
						if (isRareExclusive[i] == true)
						{
							byEntity.World.Logger.Error("Rare drop was exclusive, exiting...");
							break;
						}
                    }
                   */
				   
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }
				//else
                //{
                //    slot.Itemstack.TempAttributes.SetBool("consumed", true);
                //}
            }
        }

    }

    public class ItemLiveBivalve : Item
    {

        public AssetLocation placedBivalve;
        //public Block placedBivalve; 
        public string waterType;
		private bool debugMessages;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

                //jstack.Resolve(api.World, "Bivalve opening result");
                //AssetLocation toPlaceCode;
				bool debugMessages = Attributes["debugMessages"].AsBool(false);
                if (debugMessages == true)
                {
                    api.Logger.Error("ItemLiveBivalve debugMessages is TRUE, debug messages active!");
                }
				
                placedBivalve = new AssetLocation(this.Attributes["bivalveBlock"].AsString());
                //Block toPlaceBlock = api.World.GetBlock(placeBivalve);
                if (api.World.GetBlock(placedBivalve) != null)
                {

                    if (debugMessages) api.Logger.Error("Resolved block code {0}, for live bivalve item {1}",placedBivalve.GetName(),this.Code);
                }
                else
                {
                    if (debugMessages) api.Logger.Error("Could not resolve invalid block code '{0}' for live bivalve item {1}!",placedBivalve.GetName(),this.Code);
                    return;
                }


				
                string wcode = Attributes["waterCode"].ToString();
                if (wcode == "")
                {
                    if (debugMessages) api.Logger.Error("ItemLiveBivalve had no valid water type code, defaulting to saltwater");
                    waterType = "saltwater";
                }
                else if (wcode == "boilingwater" || wcode == "lava") 
                {
                    if (debugMessages) api.Logger.Error("ItemLiveBivalve had watercode of {0}, this would make no sense, defaulting to saltwater",wcode);
                    waterType = "saltwater";
                }
                else if (wcode != "water" && wcode != "saltwater" && wcode != "boilingwater") 
                {
                    if (debugMessages) api.Logger.Error("ItemLiveBivalve had an invalid water type code {0}, defaulting to saltwater",wcode);
                    waterType = "saltwater";
                }
                else
                {
                    if (debugMessages) api.Logger.Error("ItemLiveBivalve resolved with water code {0}",wcode);
                    waterType = wcode;
                }
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            if (debugMessages) byEntity.World.Logger.Error("Entity attempting to plant live bivalve, type: {0}", this.Code.FirstCodePart());

            Block waterBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face), BlockLayersAccess.Fluid);
            string waterBlockCode = waterBlock.LiquidCode;
            bool waterBlockValidLiquid = waterBlock != null && (waterBlockCode == "water" || waterBlockCode == "saltwater") && (waterBlockCode != "boilingwater" && waterBlockCode != "lava");
            bool waterBlockSalt = waterBlockCode == "saltwater";
            bool waterBlockGood = waterBlockCode == waterType;
            Block block = null;

            if (debugMessages) byEntity.World.Logger.Error("Attempting to plant inside water, water valid: {0}, is saltwater: {1}", waterBlockCode, waterBlockSalt);

            /*
            if (this.Code.Path.Contains("scallop") && waterBlockValid && waterBlockSalt)
            {
                byEntity.World.Logger.Error("Live bivalve was a scallop and water type was saltwater, attempting to plant!");
                block = byEntity.World.GetBlock(new AssetLocation("bivalvereef-scallop-water-establishing-free"));
            } else if (this.Code.Path.Contains("clam") && waterBlockValid && waterBlockSalt)
            {
                byEntity.World.Logger.Error("Live bivalve was a clam and water type was saltwater, attempting to plant!");
                block = byEntity.World.GetBlock(new AssetLocation("bivalvereef-clam-water-establishing-free"));
            } else if (this.Code.Path.Contains("freshwatermussel") && waterBlockValid && !waterBlockSalt)
            {
                byEntity.World.Logger.Error("Live bivalve was a freshwatermussel and water type was freshwater, attempting to plant!");
                block = byEntity.World.GetBlock(new AssetLocation("bivalvereef-freshwatermussel-water-establishing-free"));
            }
            */
            block = byEntity.World.GetBlock(placedBivalve);
            if (block == null)
            {
                if (debugMessages) byEntity.World.Logger.Error("Placed bivalve was null for some reason, backing out...");
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }
            if (!waterBlockGood || waterBlock == null)
            {
                if ((byEntity.World.Api is ICoreClientAPI capi && byEntity.Api.Side == EnumAppSide.Client))
                {
                    if (waterBlockSalt)
                    {
                        capi.TriggerIngameError(this, "needsplantinginfreshwater", Lang.Get("needsplantinginfreshwater"));
                    }
                    else
                    {
                        capi.TriggerIngameError(this, "needsplantinginsaltwater", Lang.Get("needsplantinginsaltwater"));
                    }
                    
                }
                if (debugMessages)                             byEntity.World.Logger.Error("Attempting to plant bivalves in incorrect water type, water code needed was {0}, got {1}",waterType,waterBlockCode);
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            blockSel = blockSel.Clone();
            blockSel.Position.Add(blockSel.Face);

            string useless = "";

            if (debugMessages) byEntity.World.Logger.Error("Correct water type and bivalve type found, now attempting to place block!");

            bool ok = block.TryPlaceBlock(byEntity.World, byPlayer, itemslot.Itemstack, blockSel, ref useless);

            if (ok)
            {
                if (debugMessages) byEntity.World.Logger.Error("Everything was ok, playing audio and taking an item from the player now!");
                byEntity.World.PlaySoundAt(block.Sounds.GetBreakSound(byPlayer), blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5, byPlayer);
                itemslot.TakeOut(1);
                itemslot.MarkDirty();
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
        }

    }
}
