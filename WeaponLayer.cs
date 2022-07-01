using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ID;

namespace WeaponOut;

public class WeaponLayer1 : PlayerDrawLayer
{
    public override Position GetDefaultPosition()
    {
        return new AfterParent(PlayerDrawLayers.HeldItem);
    }

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        DrawTool.InterDraw(ref drawInfo, false);
    }
}

public class WeaponLayer2 : PlayerDrawLayer
{
    public override Position GetDefaultPosition()
    {
        return new AfterParent(PlayerDrawLayers.HairBack);
    }

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        DrawTool.InterDraw(ref drawInfo, true);
    }
}

public static class DrawTool
{
    public static void InterDraw(ref PlayerDrawSet drawInfo, bool drawOnBack)
    {
        if (Main.gameMenu) return;
        //get player player
        var drawPlayer = drawInfo.drawPlayer;
        //hide if dead, stoned etc.
        if (!drawPlayer.GetModPlayer<WOPlayer>().Show) return;
        if (!drawPlayer.active || drawPlayer.dead || drawPlayer.stoned) return;
        try
        {
            if (drawPlayer.itemAnimation > 0) return;
        }
        catch
        {
            // ignored
        }

        Item heldItem = drawPlayer.inventory[drawPlayer.selectedItem];
        if (heldItem == null || heldItem.type == ItemID.None || heldItem.holdStyle != 0)
            return; //no item so nothing to show
        bool isYoyo = false;
        if (heldItem.shoot != ProjectileID.None)
        {
            if (heldItem.DamageType.CountsAsClass(DamageClass.Melee) && heldItem.noMelee)
            {
                if (Main.projectile.Where(t => t.active)
                    .Any(t => t.owner == drawPlayer.whoAmI && t.CountsAsClass(DamageClass.Melee))) return;
            }

            //  YOYO is aiStyle 99
            Projectile p = new Projectile();
            p.SetDefaults(heldItem.shoot);
            if (p.aiStyle == 99)
            {
                isYoyo = true;
            }
        }

        //item texture
        Texture2D weaponTex = weaponTex = TextureAssets.Item[heldItem.type].Value;
        if (weaponTex == null) return; //no texture to item so ignore too
        int gWidth = weaponTex.Width;
        int gHeight = weaponTex.Height;

        //does the item have an animation? No vanilla weapons do
        Rectangle? sourceRect = null;
        if (heldItem.ModItem != null)
        {
            if (Main.itemAnimations[heldItem.type] != null) // in the case of modded weapons with animations...
            {
                //get local player frame counting
                var p = drawPlayer.GetModPlayer<WOPlayer>();
                int frameCount = Main.itemAnimations[heldItem.type].FrameCount;
                int frameCounter = Main.itemAnimations[heldItem.type].TicksPerFrame * 2;

                //add them up
                if (Main.time % frameCounter == 0)
                {
                    p.WeaponFrame++;
                    if (p.WeaponFrame >= frameCount) p.WeaponFrame = 0;
                }

                //set frame on source
                gHeight /= frameCount;
                sourceRect = new Rectangle(0, gHeight * p.WeaponFrame, gWidth, gHeight);
            }
        }

        //get draw location of player
        int drawX = (int)(drawPlayer.MountedCenter.X - Main.screenPosition.X);
        int drawY = (int)(drawPlayer.MountedCenter.Y - Main.screenPosition.Y + drawPlayer.gfxOffY) - 3;
        //get the lighting on the player's tile
        Color lighting = Lighting.GetColor(
            (int)((drawInfo.Position.X + drawPlayer.width / 2f) / 16f),
            (int)((drawInfo.Position.Y + drawPlayer.height / 2f) / 16f));
        //get item alpha (like starfury) then player stealth and alpha (inviciblity etc.)
        lighting = drawPlayer.GetImmuneAlpha(heldItem.GetAlpha(lighting) * drawPlayer.stealth, 0);

        float scale = heldItem.scale;
        if (isYoyo) scale *= 0.6f;

        //standard items
        SpriteEffects spriteEffects = SpriteEffects.None;
        if (drawPlayer.direction < 0) spriteEffects = SpriteEffects.FlipHorizontally;
        if (drawPlayer.gravDir < 0)
        {
            drawY += 6;
            spriteEffects = SpriteEffects.FlipVertically | spriteEffects;
        }

        DrawData data = new DrawData(
            weaponTex,
            new Vector2(drawX, drawY),
            sourceRect,
            lighting,
            0f,
            new Vector2(gWidth / 2f, gHeight / 2f),
            scale,
            spriteEffects,
            0);

        // if (WeaponOut.itemCustomizer != null)
        // {
        //     data.shader = ItemCustomizerGetShader(WeaponOut.itemCustomizer, heldItem);
        // }

        float itemWidth = gWidth * heldItem.scale;
        float itemHeight = gHeight * heldItem.scale;
        //not all items have width/height set the same, so use largest as "length" including weapon sizemod
        float larger = Math.Max(itemWidth, itemHeight);
        float lesser = Math.Min(itemWidth, itemHeight);
        PickItemDrawType(drawOnBack, drawPlayer, heldItem, isYoyo, gWidth, gHeight, ref data, itemWidth,
            itemHeight, larger, lesser, drawInfo);
    }

    public enum HoldType
    {
        None,
        Hand,
        Waist,
        Back,
        Spear,
        PowerTool,
        Bow,
        SmallGun,
        LargeGun,
        Staff
    }

    private static void PickItemDrawType(bool drawOnBack, Player drawPlayer, Item heldItem, bool isYoyo, int gWidth,
        int gHeight, ref DrawData data, float itemWidth, float itemHeight, float larger, float lesser,
        PlayerDrawSet drawInfo)
    {
        HoldType holdType = HoldType.None;

        #region AutoPicker

        if (heldItem.useStyle == 1 || //swing
            heldItem.useStyle == 2 || //eat
            heldItem.useStyle == 3) //stab
        {
            //|       ######        
            //|       ##  ##        
            //|     ##########            
            //|       ##  ##    
            //|       ##  ##    
            //|       ##  ##    
            //|       ##  ##    
            //|         ##      
            //Items, daggers and other throwables lie below 28 and are easily held in the hand
            ;
            if ((larger < 28 && !(heldItem.DamageType == DamageClass.Magic)) || //nonmagic weapons
                (larger <= 32 && heldItem.shoot != 0) || //larger for throwing weapons
                (larger <= 24 && heldItem.DamageType != DamageClass.Magic)) //only smallest magic weapons
            {
                if (drawPlayer.grapCount > 0) return; // can't see while grappling
                if (drawOnBack) return;
                holdType = HoldType.Hand;
            }
            //|             ####
            //|           ##  ##
            //|         ##  ##   
            //|       ##  ##    
            //|   ####  ##      
            //|   ##  ##        
            //| ##  ####        
            //| ####            
            //Broadsword weapons are swing type weapons between 28 - 48
            //They are worn on the waist, and react to falling! Except when disabled
            //This also amusingly applies to ducks, axes and rockfish
            //But shouldn't apply to pickaxes, except when they are also not pickaxes
            else if (larger <= 48 &&
                     (heldItem.pick <= 0 ||
                      (heldItem.pick > 0 && heldItem.axe > 0)))
            {
                if (!drawOnBack) return;
                holdType = HoldType.Waist;
            }
            //|           ########
            //|           ##    ##
            //|         ##    ####
            //|   ##  ##    ##  
            //|   ####    ##    
            //|   ##  ####      
            //| ##  ########    
            //| ######          
            //Great weapons are swing type weapons past 36 in size and slung on the back
            else
            {
                if (!drawOnBack) return;
                holdType = HoldType.Back;
            }
        }

        if (heldItem.useStyle == 4 || //hold up
            heldItem.useStyle == 5) //hold out
        {
            bool isAStaff = Item.staff[heldItem.type];
            //staves, guns and bows
            if (gHeight >= gWidth * 1.2f && !isAStaff)
            {
                //|    ######       
                //|    ##  ######   
                //|    ##    ##  ##  
                //|    ##    ##  ## 
                //|    ##    ##  ## 
                //|    ##    ##  ## 
                //|    ##  ######   
                //|    ######       
                //bows
                if (drawPlayer.grapCount > 0) return; // can't see while grappling
                if (drawOnBack) return;
                holdType = HoldType.Bow;
            }
            else if (gWidth >= gHeight * 1.2f && !isAStaff)
            {
                if (heldItem.noUseGraphic && heldItem.CountsAsClass(DamageClass.Melee))
                {
                    //|                 
                    //|    ####         
                    //|  ##  ########## 
                    //|  ####    ##    ####
                    //|  ##  ##  ##        ####
                    //|  ##      ##  ######
                    //|    ############ 
                    //|                 
                    //drills, chainsaws
                    if (drawPlayer.grapCount > 0) return; // can't see while grappling
                    if (drawOnBack) return;
                    holdType = HoldType.PowerTool;
                }
                else
                {
                    if (larger < 45)
                    {
                        //| ####        ####
                        //| ##  ########  ##
                        //|   ####        ##
                        //|   ##    ########
                        //|   ##  ##  ##      
                        //|   ##  ####        
                        //|   ######          
                        //|                 
                        if (drawPlayer.grapCount > 0) return; // can't see while grappling
                        if (drawOnBack) return;
                        //small aimed weapons (like handgun/aquasceptre) held halfway down, 1/3 back
                        holdType = HoldType.SmallGun;
                    }
                    else
                    {
                        //|                 
                        //|               ##
                        //| ######################
                        //| ##  ##      ##  ##
                        //| ##  ############
                        //| ####  ##    ##  
                        //|     ####    ##  
                        //|                 
                        if (drawOnBack) return;
                        //large guns (rifles, launchers, etc.) held with both hands
                        holdType = HoldType.LargeGun;
                    }
                }
            }
            else
            {
                if (heldItem.noUseGraphic && !isAStaff)
                {
                    if (!heldItem.autoReuse)
                    {
                        if (drawPlayer.grapCount > 0) return; // can't see while grappling
                        if (drawOnBack) return;
                        if (isYoyo)
                        {
                            //sam (?why did i write sam? maybe same?)
                            data = WeaponDrawInfo.modDraw_HandWeapon(data, drawPlayer, larger, lesser, isYoyo);
                        }
                        else
                        {
                            //|             ####
                            //|         ####  ##
                            //|       ##    ##  
                            //|         ##  ##  
                            //|       ##  ##    
                            //|     ##          
                            //|   ##            
                            //| ##              
                            //spears are held facing to the floor, maces generally held
                            holdType = HoldType.Spear;
                        }
                    }
                    else
                    {
                        //nebula blaze, flairon, solar eruption (too inconsistent)
                        if (larger <= 48)
                        {
                            if (!drawOnBack) return;
                            holdType = HoldType.Waist;
                        }
                        else
                        {
                            if (!drawOnBack) return;
                            holdType = HoldType.Back;
                        }
                    }
                }
                else
                {
                    if (larger + lesser <= 72) //only smallest magic weapons
                    {
                        //|         ######  
                        //|       ##  ##  ##
                        //|     ##      ##  ##
                        //|   ##        ######
                        //| ##        ##  ##
                        //| ##      ##  ##  
                        //|   ##  ##  ##    
                        //|     ######
                        if (drawPlayer.grapCount > 0) return; // can't see while grappling
                        if (drawOnBack) return;
                        holdType = HoldType.Hand;
                    }
                    else if (lesser <= 42) //medium sized magic weapons, treated like polearms
                    {
                        if (drawPlayer.grapCount > 0) return; // can't see while grappling
                        if (drawOnBack) return;
                        holdType = HoldType.Spear;
                    }
                    else
                    {
                        //largestaves are held straight up
                        //|                 
                        //|             ####
                        //|   ############  ##
                        //| ##        ##      ##
                        //|   ############  ##
                        //|             ####
                        //|                 
                        //|                 
                        if (drawPlayer.grapCount > 0) return; // can't see while grappling
                        if (drawOnBack) return;
                        //staves
                        holdType = HoldType.Staff;
                    }
                }
            }
        }

        #endregion

        switch (holdType)
        {
            case HoldType.Hand:
                data = WeaponDrawInfo.modDraw_HandWeapon(data, drawPlayer, larger, lesser);
                break;
            case HoldType.Waist:
                data = WeaponDrawInfo.modDraw_WaistWeapon(data, drawPlayer, larger);
                break;
            case HoldType.Spear:
                data = WeaponDrawInfo.modDraw_PoleWeapon(data, drawPlayer, larger);
                break;
            case HoldType.PowerTool:
                data = WeaponDrawInfo.modDraw_DrillWeapon(data, drawPlayer, larger);
                break;
            case HoldType.Back:
                data = WeaponDrawInfo.modDraw_BackWeapon(data, drawPlayer, larger);
                break;
            case HoldType.Bow:
                data = WeaponDrawInfo.modDraw_ForwardHoldWeapon(data, drawPlayer, lesser);
                break;
            case HoldType.SmallGun:
                data = WeaponDrawInfo.modDraw_AimedWeapon(data, drawPlayer, larger);
                break;
            case HoldType.LargeGun:
                data = WeaponDrawInfo.modDraw_HeavyWeapon(data, drawPlayer, lesser);
                break;
            case HoldType.Staff:
                data = WeaponDrawInfo.modDraw_MagicWeapon(data, drawPlayer, larger);
                break;
            default: return;
        }

        drawInfo.DrawDataCache.Add(data);
        WeaponDrawInfo.drawGlowLayer(data, drawPlayer, heldItem, drawInfo);
    }

    // private static int ItemCustomizerGetShader(Mod mod, Item item)
    // {
    //     if (!Main.dedServ)
    //     {
    //         try
    //         {
    //           
    //             GlobalItem cii = item.GetGlobalItem(mod, "CustomizerItem");
    //
    //             // The field we're looking for
    //             var shaderIDInfo = cii.GetType().GetField("shaderID");
    //
    //             // Check this field on this class
    //             int shaderID = (int)shaderIDInfo.GetValue(cii);
    //
    //             // We got this
    //             return shaderID;
    //         }
    //         catch
    //         {
    //         }
    //     }
    //
    //     return 0;
    // }
}