using Vintagestory.API.Client;
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
        private int minDepth;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            minDepth = this.Attributes["minDepth"].AsInt(2);
            maxDepth = this.Attributes["maxDepth"].AsInt(6);

			if (Variant["state"] == "harvested") return;

            // TO_OPTIMIZE: Probably can re-use a vanilla cache of knives
            ObjectCacheUtil.GetOrCreate(api, "bivalveBlockInteractions", () =>
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

            if (belowBlock.Fertility > 0)
            {
                Block placingBlock = blockAccessor.GetBlock(Code);
                if (placingBlock == null) return false;
                
                blockAccessor.SetBlock(placingBlock.BlockId, pos);
                return true;
            }

            if(belowBlock.LiquidCode == Attributes["waterCode"].ToString())
            {
                for(var currentDepth = 0; currentDepth <= maxDepth; currentDepth ++)
                {
                    belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - currentDepth, pos.Z);
                    if (belowBlock.Fertility > 0)
                    {
                        if (currentDepth < minDepth)
                        {
                            return false;
                        }
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
			if (Variant["state"] == "harvested") dt /= 2;
            else if (player.InventoryManager.ActiveTool != EnumTool.Knife)
            {
                dt /= 3;
            }
            else
            {
                float mul;
                if (itemslot.Itemstack.Collectible.MiningSpeed.TryGetValue(EnumBlockMaterial.Plant, out mul)) dt *= mul;
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
		
		public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            Block harvestedBlock = api.World.GetBlock(CodeWithVariant("state", "harvested"));
            Block grownBlock = api.World.GetBlock(CodeWithVariant("state", "normal"));

            return grownBlock.Drops.Append(harvestedBlock.Drops);
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
            // Should this be returning the bivalve knife list?
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }
		
    }

    public class ItemBivalve : Item
    {
        float curX;
        float curY;
        
        float prevSecUsed;
		float processingSecRequired;
        LCGRandom rnd;

        ItemStack[] processingResultStacks;
        ItemStack[] rareProcessingResultStacks;
        double[] rareProcessingResultChances;
		bool[] isRareResultExclusive;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            rnd = new LCGRandom(api.World.Seed);
            JsonItemStack[] pstacks = Attributes["processingResults"].AsObject<JsonItemStack[]>();
            List<ItemStack> stacklist = new List<ItemStack>();
            for (int i = 0; i < pstacks.Length; i++)
            {
                JsonItemStack jstack = pstacks[i];
                jstack.Resolve(api.World, "Bivalve opening result");
                if (jstack.ResolvedItemstack != null)
                {
                    stacklist.Add(jstack.ResolvedItemstack);
                }
                else api.Logger.Warning("Unable to resolve itemstack " + jstack.Code + " for " + Code);
            }
            processingResultStacks = stacklist.ToArray();
            
            processingSecRequired = Attributes["processingTime"].AsFloat(1.2f);

            // Consider moving these to BlockDropItemstack and making use of the lastDrop field
            pstacks = Attributes["rareProcessingResultStacks"].AsObject<JsonItemStack[]>();

            stacklist = new List<ItemStack>();
            if (pstacks != null && pstacks.Length > 0)
            {
                rareProcessingResultChances = Attributes["rareProcessingResultChances"].AsObject<Double[]>();

                List<float> chancelist = new List<float>();  
                for (int i = 0; i < pstacks.Length; i++)
                {
                    JsonItemStack jstack = pstacks[i];
                    jstack.Resolve(api.World, "Bivalve rare opening result");
                    if (jstack.ResolvedItemstack != null)
                    {
                        stacklist.Add(jstack.ResolvedItemstack);
                    }
                    else api.Logger.Warning("Unable to resolve itemstack " + jstack.Code + " for " + Code);
                }
                rareProcessingResultStacks = stacklist.ToArray();
                
                bool[] resultXRaw = Attributes["rareProcessingResultExclusive"].AsObject<bool[]>();
                
                // Check to see if isRareResultExclusive is the same length as the rareProccessingResults
                if (resultXRaw.Length != rareProcessingResultStacks.Length)
                {
                    bool[] resultX = new bool[rareProcessingResultStacks.Length];
                    for (int i = 0; i < pstacks.Length; i++)
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
                rareProcessingResultStacks = new ItemStack[0];
                rareProcessingResultChances = new Double[0];
                if (pstacks != null || true) api.Logger.Warning("rareProcessingResults length is zero for item " + Code);
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (slot.Itemstack.TempAttributes.GetBool("consumed") == true) return;

            handling = EnumHandHandling.PreventDefault;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            // Todo: Get some audio for shucking the scallops/clams/mussels
            // byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/messycraft"), byPlayer, byPlayer);
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
                byEntity.Controls.UsingHeldItemTransformBefore = tf;

                curX = nowx;
                curY = nowy;

                prevSecUsed = secondsUsed;
            }

            if (api.World.Side == EnumAppSide.Server) return true;

            return secondsUsed < processingSecRequired + 0.1f;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed > processingSecRequired)
            {
                if (api.Side == EnumAppSide.Server)
                {
                    // Handle processing results - shells, meat, other mundane items
                    for (int i = 0; i < processingResultStacks.Length; i++)
                    {
                        ItemStack resultstack = processingResultStacks[i];
                        if (!byEntity.TryGiveItemStack(resultstack))
                        {
                            byEntity.World.SpawnItemEntity(resultstack, byEntity.Pos.XYZ.Add(0, 0.5, 0));
                        }
                    }

                    List<ItemStack> rareStacks = ((ItemStack[])rareProcessingResultStacks?.Clone()).ToList();
                    List<double> rareChances = rareProcessingResultChances?.ToList();
                    List<bool> isRareExclusive = isRareResultExclusive?.ToList();
                    while (rareStacks != null && rareStacks.Count > 0)
                    {
                        int i = byEntity.World.Rand.Next(rareStacks.Count);
                        double roll = byEntity.World.Rand.NextDouble();
                        if (roll < rareChances[i])
                        {
                            ItemStack resultstack = rareStacks[i];
                            if (!byEntity.TryGiveItemStack(resultstack))
                            {
                                byEntity.World.SpawnItemEntity(resultstack, byEntity.Pos.XYZ.Add(0, 0.5, 0));
                            }
						    if (isRareExclusive[i] == true)
						    {
							    break;
						    }
                        }
                        rareStacks.RemoveAt(i);
                        rareChances.RemoveAt(i);
                        isRareExclusive.RemoveAt(i);
                    }
                   
                    slot.TakeOut(1);
                    slot.MarkDirty();
                } else
                {
                    slot.Itemstack.TempAttributes.SetBool("consumed", true);
                }
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-openbivalve",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }
    }

    public class ItemLiveBivalve : Item
    {

        public AssetLocation placedBivalve;
        public string waterType;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

                placedBivalve = new AssetLocation(this.Attributes["bivalveBlock"].AsString());
                if (api.World.GetBlock(placedBivalve) == null)
                {
                    api.Logger.Error("Could not resolve invalid block code '{0}' for live bivalve item {1}!",placedBivalve.GetName(), this.Code);
                    return;
                }

                string wcode = Attributes["waterCode"].ToString();
                if (wcode == "")
                {
                    api.Logger.Warning("ItemLiveBivalve had no valid water type code, defaulting to saltwater");
                    waterType = "saltwater";
                }
                else if (wcode == "boilingwater" || wcode == "lava") 
                {
                    api.Logger.Error("ItemLiveBivalve had watercode of {0}, this would make no sense, defaulting to saltwater", wcode);
                    waterType = "saltwater";
                }
                else if (wcode != "water" && wcode != "saltwater" && wcode != "boilingwater") 
                {
                    api.Logger.Error("ItemLiveBivalve had an invalid water type code {0}, defaulting to saltwater", wcode);
                    waterType = "saltwater";
                }
                else
                {
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

            Block waterBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face), BlockLayersAccess.Fluid);
            string waterBlockCode = waterBlock.LiquidCode;
            bool waterBlockValidLiquid = waterBlock != null && (waterBlockCode == "water" || waterBlockCode == "saltwater") && (waterBlockCode != "boilingwater" && waterBlockCode != "lava");
            bool waterBlockSalt = waterBlockCode == "saltwater";
            bool waterBlockGood = waterBlockCode == waterType;
            Block block = null;

            block = byEntity.World.GetBlock(placedBivalve);
            if (block == null)
            {
                byEntity.World.Logger.Error("Bivalve block was null for code " + placedBivalve);
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
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            blockSel = blockSel.Clone();
            blockSel.Position.Add(blockSel.Face);

            string useless = "";

            bool ok = block.TryPlaceBlock(byEntity.World, byPlayer, itemslot.Itemstack, blockSel, ref useless);

            if (ok)
            {
                byEntity.World.PlaySoundAt(block.Sounds.GetBreakSound(byPlayer), blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5, byPlayer);
                itemslot.TakeOut(1);
                itemslot.MarkDirty();
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    HotKeyCode = "shift",
                    ActionLangCode = "heldhelp-plant",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }

    }
}
