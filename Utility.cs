﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;

namespace FarmhandFinder
{
    public static class Utility
    {
        // TODO: Pixel size is ~1.5x larger than that of other UI elements at high zoom levels (150%)?
        /// <summary>
        /// Draws the specified texture properly scaled to the in-game UI scaling option's value.
        /// </summary>
        /// <param name="spriteBatch">The spritebatch to draw to.</param>
        /// <param name="texture">The texture to draw--assumes a square size.</param>
        /// <param name="position">The position at which the sprite will be drawn. The sprite will be centered about
        /// this position</param>
        /// <param name="scale">The scale at which the sprite will be drawn.</param>
        /// <param name="angle">The angle at which the sprite will be drawn.</param>
        public static void DrawUiSprite(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, float scale, float angle, float alpha)
        {
            int width = texture.Width, height = texture.Height;
            spriteBatch.Draw(
                texture, position, new Rectangle(0, 0, width, height), Color.White * alpha, angle, 
                new Vector2(width / 2f, height / 2f), 4 * scale * Game1.options.uiScale, SpriteEffects.None, 0.8f);
        }



        /// <summary>
        /// Draws the specified farmers head sprite. If the farmer is not yet fully loaded, no sprite will be drawn.
        /// </summary>
        /// <param name="spriteBatch">The spritebatch to draw to.</param>
        /// <param name="farmer">The specified farmer which the sprite will represent.</param>
        /// <param name="position">The position at which the sprite will be drawn. The sprite will be centered about
        /// this position.</param>
        /// <param name="scale">The scale at which the sprite will be drawn.</param>
        public static void DrawFarmerHead(SpriteBatch spriteBatch, Farmer farmer, Vector2 position, float scale)
        {
            // The constants for the origin are chosen such that the head lines up just above the third-most bottom
            // pixel of the background sprite. I'm not sure how to make the calculation cleaner...
            var origin = new Vector2(8f, (1 + scale) / 2 * (10.5f + (farmer.IsMale ? 0 : 1)));

            float layerDepth = 0.8f;
            FarmerSprite.AnimationFrame animationFrame = new FarmerSprite.AnimationFrame(0, 0, false, false, null, false);
            Vector2 rotationAdjustment = Vector2.Zero;
            Vector2 positionOffset = new Vector2((float)(animationFrame.positionOffset * 4), (float)(animationFrame.xOffset * 4));
            float rotation = 0;
            Color overrideColor = Color.White;

            // Get the base texture of the target farmer--it will include the skin color, eye color, etc.
            var baseTexture = FarmhandFinder.Instance.Helper.Reflection.GetField<Texture2D>(
                farmer.FarmerRenderer, "baseTexture").GetValue();

            void DrawFarmerFace()
            {
                // headSourceRect corresponds to the front facing sprite.
                var headSourceRect = new Rectangle(0, 0, 16, farmer.IsMale ? 15 : 16);

                spriteBatch.Draw(
                    baseTexture, 
                    position, 
                    headSourceRect, 
                    overrideColor, 
                    rotation, origin, 4 * scale, animationFrame.flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, FarmerRenderer.GetLayerDepth(layerDepth, FarmerRenderer.FarmerSpriteLayers.Base, false));
            }

            
            Color hair_color = farmer.hairstyleColor.Value;
            if (farmer.prismaticHair.Value)
            {
                hair_color = StardewValley.Utility.GetPrismaticColor(0, 1f);
            }

            void DrawFarmerAccessories()
            {
                FarmerRenderer.FarmerSpriteLayers accessoryLayer = FarmerRenderer.FarmerSpriteLayers.Accessory;
                if (farmer.accessory.Value >= 0 && farmer.FarmerRenderer.drawAccessoryBelowHair(farmer.accessory.Value))
                {
                    accessoryLayer = FarmerRenderer.FarmerSpriteLayers.AccessoryUnderHair;
                } 

                var accessorySourceRect = new Rectangle(
                    farmer.accessory.Value * 16 % FarmerRenderer.accessoriesTexture.Width,
                    farmer.accessory.Value * 16 / FarmerRenderer.accessoriesTexture.Width * 32, 
                    16, 16);

                var accessoryOffset = new Vector2(0, -3f);

                spriteBatch.Draw(FarmerRenderer.accessoriesTexture,
                    position + accessoryOffset + positionOffset + rotationAdjustment + new Vector2((float)(FarmerRenderer.featureXOffsetPerFrame[0] * 4), (float)(8 + FarmerRenderer.featureYOffsetPerFrame[0] * 4 + farmer.FarmerRenderer.heightOffset.Value - 4)), // i cant figure out why should -2
                    accessorySourceRect, 
                    (overrideColor.Equals(Color.White) && farmer.FarmerRenderer.isAccessoryFacialHair(farmer.accessory.Value)) ? hair_color : overrideColor, 
                    rotation, origin, 4f * scale, SpriteEffects.None, FarmerRenderer.GetLayerDepth(layerDepth, accessoryLayer, false));
            }

            void DrawFarmerHair(int bottomOffset)
            {
                var hair_style = farmer.getHair(false);
                var hairStyleMetadata = Farmer.GetHairStyleMetadata(hair_style);
                
                // Logic for hair shown beneath a hat.
                if (farmer.hat.Value != null && farmer.hat.Value.hairDrawType.Value == 1 && 
                    hairStyleMetadata != null && hairStyleMetadata.coveredIndex != -1)
                {
                    hair_style = hairStyleMetadata.coveredIndex;
                    hairStyleMetadata = Farmer.GetHairStyleMetadata(hair_style);
                }
                
                var hairstyleTexture = FarmerRenderer.hairStylesTexture;
                // We factor in an 'offset' to ensure that the hair does not clip outside of the background.
                var hairstyleSourceRect = new Rectangle(
                    hair_style * 16 % FarmerRenderer.hairStylesTexture.Width,
                    hair_style * 16 / FarmerRenderer.hairStylesTexture.Width * 96, 
                    16, 32 - bottomOffset);
                
                if (hairStyleMetadata != null)
                {
                    hairstyleTexture = hairStyleMetadata.texture;
                    hairstyleSourceRect = new Rectangle(
                        hairStyleMetadata.tileX * 16, hairStyleMetadata.tileY * 16, 
                        16, 32 - bottomOffset);
                }

                spriteBatch.Draw(
                    hairstyleTexture, 
                    position + positionOffset + new Vector2((float)(FarmerRenderer.featureXOffsetPerFrame[0] * 4), (float)(FarmerRenderer.featureYOffsetPerFrame[0] * 4 + ((farmer.IsMale && farmer.hair.Value >= 16) ? -4 : ((!farmer.IsMale && farmer.hair.Value < 16) ? 4 : 0)))),
                    hairstyleSourceRect, 
                    overrideColor.Equals(Color.White) ? hair_color : overrideColor, 
                    rotation, origin, 4f * scale, SpriteEffects.None, FarmerRenderer.GetLayerDepth(layerDepth, FarmerRenderer.FarmerSpriteLayers.Hair, false));
            }
            
            void DrawFarmerHat(int sideOffset, int topOffset, int bottomOffset)
            {
                ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(farmer.hat.Value.QualifiedItemId);
                int spriteIndex = itemData.SpriteIndex;
                var hatOrigin = origin - new Vector2(0, farmer.IsMale ? 0 : 1);
                bool flip2 = farmer.FarmerSprite.CurrentAnimationFrame.flip;
                // We factor in an 'offset' to ensure that the hair does not clip outside of the background.
                var hatSourceRect = new Rectangle(
                    20 * spriteIndex % FarmerRenderer.hatsTexture.Width + sideOffset,
                    20 * spriteIndex / FarmerRenderer.hatsTexture.Width * 20 * 4 + topOffset, 
                    20 - 2 * sideOffset, 20 - topOffset - bottomOffset);

                var hatOffset = new Vector2(0, -5f);

                spriteBatch.Draw(
                    FarmerRenderer.hatsTexture,
                    position + hatOffset + positionOffset + new Vector2(
                        (float)(-8 + (flip2 ? -1 : 1) * FarmerRenderer.featureXOffsetPerFrame[0] * 4 + (4 * sideOffset)), 
                        (float)(-16 + FarmerRenderer.featureYOffsetPerFrame[0] * 4 + (4 * topOffset) + (farmer.hat.Value.ignoreHairstyleOffset.Value ? 0 : FarmerRenderer.hairstyleHatOffset[farmer.hair.Value % 16]) + 4 + farmer.FarmerRenderer.heightOffset.Value)),
                    hatSourceRect,
                    farmer.hat.Value.isPrismatic.Value ? StardewValley.Utility.GetPrismaticColor(0, 1f) : overrideColor,
                    rotation, hatOrigin, 4f * scale, SpriteEffects.None, FarmerRenderer.GetLayerDepth(layerDepth, FarmerRenderer.FarmerSpriteLayers.Hat, false));
            }

            // Only draw the head if the base texture has been generated.
            if (baseTexture == null) return;
            
            // Note: Accessories are facial hair and glasses.
            DrawFarmerFace();
            if (farmer.accessory.Value >= 0) 
                DrawFarmerAccessories();
            
            // Offset values are chosen roughly on what can fit with a 0.75 scale value--they must be manually changed
            // otherwise.
            DrawFarmerHair(16);
            // Note: A decision was made not to render the farmer's 'normal' hairstyle when the farmer is in a bathing
            //       suit as it may be confusing for identifying the farmer.
            if (farmer.hat.Value != null) 
                DrawFarmerHat(2, 3, 2);
        }

        
        
        /// <summary>
        /// Checks if a UI element (either stamina/health bar, clock box, or toolbar) exists at the specified position.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <returns>Whether the position resides within a UI element.</returns>
        public static bool UiElementsIntersect(Vector2 position)
        {
            bool IntersectsStaminaHealthBar()
            {
                var topOffset = (int)(Math.Max(Game1.player.MaxStamina, Game1.player.maxHealth) * 0.625f);
                var leftBound = Game1.graphics.GraphicsDevice.Viewport.TitleSafeArea.Right - 56;
                var topBound = Game1.graphics.GraphicsDevice.Viewport.TitleSafeArea.Bottom - topOffset - 72;
                
                if (Game1.showingHealthBar) leftBound -= 56;

                return position.X > leftBound && position.Y > topBound;
            }
            
            bool IntersectsClockBox()
            {
                var clockRect = new Rectangle(
                    Game1.dayTimeMoneyBox.position.ToPoint(), 
                    new Point(
                        ((IClickableMenu)Game1.dayTimeMoneyBox).width, 
                        ((IClickableMenu)Game1.dayTimeMoneyBox).height));
                return clockRect.Contains(position);
            }

            bool IntersectsToolbar()
            {
                var toolbar = Game1.onScreenMenus.OfType<Toolbar>().FirstOrDefault();
                return toolbar?.isWithinBounds((int)position.X, (int)position.Y) ?? false;
            }

            return IntersectsStaminaHealthBar() || IntersectsClockBox() || IntersectsToolbar();
        }
        
        
        
        /// <summary>
        /// Get the intersection point between a line segment and rectangle. Assumes that the first endpoint lies within
        /// the bounds of the rectangle and that an intersection point exists. <br/><br/>
        /// * Algorithm adapted from Daniel White https://www.skytopia.com/project/articles/compsci/clipping.html
        /// </summary>
        /// <param name="p1">The first line segment endpoint--it should lie within the bounds of the rectangle</param>
        /// <param name="p2">The second line segment endpoint.</param>
        /// <param name="r">The rectangle used for intersection.</param>
        /// <param name="offset">An offset applied to each side of the rectangle effectively 'shrinking' it.</param>
        /// <returns>The intersection point.</returns>
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public static Vector2 LiangBarskyIntersection(Vector2 p1, Vector2 p2, xTile.Dimensions.Rectangle r, int offset)
        {
            float t = 1f, o = offset * Game1.options.uiScale / Game1.options.zoomLevel;
            float minX = r.X + o, 
                  minY = r.Y + o, 
                  maxX = r.X + r.Width - o, 
                  maxY = r.Y + r.Height - o;
            
            // Iterate over the sides of the rectangle.
            float dx = p2.X - p1.X, dy = p2.Y - p1.Y;
            for (var i = 0; i < 4; i++) 
            {
                float p = 0f, q = 0f;
                switch (i)
                {
                    case 0: p = -dx; q = p1.X - minX; break; // Left side.
                    case 1: p =  dx; q = maxX - p1.X; break; // Bottom side.
                    case 2: p = -dy; q = p1.Y - minY; break; // Right side.
                    case 3: p =  dy; q = maxY - p1.Y; break; // Top side.
                }

                var u = q / p;
                if (p > 0 && u < t) t = u;
            }
            
            return new Vector2(p1.X + t * dx, p1.Y + t * dy);
        }



        /// <summary>
        /// Gets the intersection point between the player, farmer, and viewport and calculates the compass arrow angle.
        /// </summary>
        /// <param name="targetFarmer">The farmer to which the player and viewport will check for intersection.</param>
        /// <param name="intersection">The intersection point between the player, farmer, and viewport--will be set to
        /// a zeroed Vector2 if an intersection could not be found.</param>
        /// <param name="arrowAngle">The angle at which the compass arrow is to be drawn--will be set to 0 if an
        /// intersection could not be found.</param>
        /// <returns>Whether an intersection point was found.</returns>
        public static bool HandleIntersectionCalculations(
            Farmer targetFarmer, out Vector2 intersection, out float arrowAngle)
        {
            intersection = Vector2.Zero; arrowAngle = 0f;
            var playerCenter = Game1.player.Position + new Vector2(0.5f * Game1.tileSize, -0.5f * Game1.tileSize);
            
            // TODO: Maybe use farmer.getBoundingBox() in some way rather than make our own?
            // We denote an approximate bound about the farmer before checking if the approximate bound intersects
            // the viewport--if so, the peer is on screen and should be skipped. If not, then a line drawn between
            // the player and peer definitely intersects the viewport draw bounds.
            var peerBounds = new xTile.Dimensions.Rectangle(
                (int)(targetFarmer.position.X + 0.125f * Game1.tileSize), 
                (int)(targetFarmer.position.Y - 1.5f * Game1.tileSize), 
                (int)(0.75f * Game1.tileSize), 2 * Game1.tileSize);
                
            var peerCenter = new Vector2(peerBounds.X + peerBounds.Width / 2f, peerBounds.Y + peerBounds.Height / 2f);
                
            // Skip if bounds and viewport intersect.
            if (peerBounds.Intersects(Game1.viewport)) return false;
            
            // As we now know that there is a definite intersection between the player, peer, and viewport,
            // calculate the respective intersection point.
            intersection = LiangBarskyIntersection(
                playerCenter, peerCenter, Game1.viewport, FarmhandFinder.Config.HideCompassArrow ? 40 : 50);
                
            arrowAngle = (float) Math.Atan2(peerCenter.Y - intersection.Y, peerCenter.X - intersection.X);
            // Normalized position based on the viewport, zoom level, and UI scale.
            intersection = (intersection - new Vector2(Game1.viewport.X, Game1.viewport.Y)) 
                * Game1.options.zoomLevel / Game1.options.uiScale;
            
            return true;
        }
    }
}