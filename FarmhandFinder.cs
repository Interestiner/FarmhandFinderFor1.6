using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using GenericModConfigMenu;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace FarmhandFinder
{
    public class FarmhandFinder : Mod
    {
        internal static FarmhandFinder Instance { get; private set; }
        internal static ModConfig Config;
        
        internal static Texture2D BackgroundTexture;
        internal static Texture2D ForegroundTexture;
        internal static Texture2D ArrowTexture;
        
        internal static readonly Dictionary<long, CompassBubble> CompassBubbles = new();

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Instance = this; Config = Helper.ReadConfig<ModConfig>();
            if (Config.Alpha < 0)
            {
                Config.Alpha = 0;
            }
            if (Config.Alpha > 100)
            {
                Config.Alpha = 100;
            }
            LoadTextures(helper);
            
            // If not all options are disabled, we have work to do.
            if (!Config.HideCompassBubble || !Config.HideCompassArrow)
                helper.Events.Display.RenderedHud += OnRenderedHud;

            if (!Config.HideCompassBubble)
                HandleCompassBubbles(helper);

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(Config)
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Hide Compass Bubble",
                getValue: () => Config.HideCompassBubble,
                setValue: value => Config.HideCompassBubble = value
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Hide Compass Bubble",
                getValue: () => Config.HideCompassArrow,
                setValue: value => Config.HideCompassArrow = value
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Alpha",
                getValue: () => Config.Alpha,
                setValue: (int val) => {
                    Config.Alpha = val;
                },
                min: 0,
                max: 100
            );
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Loads the background, foreground, and arrow textures.</summary>
        /// <param name="helper">IModHelper instance for loading data.</param>
        private static void LoadTextures(IModHelper helper)
        {
            BackgroundTexture = helper.ModContent.Load<Texture2D>("assets/bubble.png");
            ForegroundTexture = helper.ModContent.Load<Texture2D>("assets/bubble_front.png");
            ArrowTexture      = helper.ModContent.Load<Texture2D>("assets/arrow.png");
        }
        
        
        
        /// <summary>Handles the generation and deletion of compass bubble instances.</summary>
        /// <param name="helper">IModHelper instance to add event predicates.</param>
        private void HandleCompassBubbles(IModHelper helper)
        {
            helper.Events.GameLoop.OneSecondUpdateTicked += (_, _) =>
            {
                // Generate a corresponding compass bubble and add to dictionary if one has not been created yet.
                foreach (var peer in Helper.Multiplayer.GetConnectedPlayers())
                {
                    var farmer = Game1.getFarmer(peer.PlayerID);
                    if (CompassBubbles.ContainsKey(farmer.UniqueMultiplayerID) && object.ReferenceEquals(CompassBubbles[farmer.UniqueMultiplayerID].GetFarmer(), farmer))
                        continue;
                        //CompassBubbles.Remove(farmer.UniqueMultiplayerID);
                    
                    CompassBubbles[farmer.UniqueMultiplayerID] = new CompassBubble(farmer, helper);
                }
            };

            // If a peer disconnects from the world, remove their respective dictionary entry.
            // TODO: This doesn't seem to work properly at the moment?
            helper.Events.Multiplayer.PeerDisconnected += (_, e) => CompassBubbles.Remove(e.Peer.PlayerID);

            // If the game returns to the title screen, clear the compass bubble dictionary.
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => CompassBubbles.Clear();
        }
        


        /// <summary>
        /// Raised after drawing the HUD (item toolbar, clock, etc) to the sprite batch, but before it's rendered to
        /// the screen.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            // Render nothing if no remote players are present or if the player is in a cutscene.
            if (!Context.HasRemotePlayers || Game1.player.hidden.Value) return;

            foreach (var peer in Helper.Multiplayer.GetConnectedPlayers())
            {
                var farmer = Game1.getFarmer(peer.PlayerID);
                
                var sameLocation = farmer.currentLocation != null &&
                                   farmer.currentLocation.Equals(Game1.player.currentLocation);
                
                // TODO: More split screen checks are needed--specifically having the peer bubble show up in both
                // TODO: screens and displayed on the correct location for each screen.
                // Render nothing if the peer is not in the same location or is hidden (cutscene).
                if (peer.IsSplitScreen || !sameLocation || farmer.hidden.Value) continue;
                
                // Also render nothing if an intersection between the player, peer, and viewport does not exist.
                if (!Utility.HandleIntersectionCalculations(farmer, out var compassPos, out var arrowAngle)) continue;

                // Only draw the compass bubble if one has already been generated.
                if (!Config.HideCompassBubble && CompassBubbles.ContainsKey(farmer.UniqueMultiplayerID))
                {
                    var alpha = Math.Min(Utility.UiElementsIntersect(compassPos) ? 0.5f : 1f, Config.Alpha / 100.0f);
                    
                    // Drawing the compass bubble at the normalized position.
                    CompassBubbles[farmer.UniqueMultiplayerID].Draw(e.SpriteBatch, compassPos, 1, alpha);
                }
                
                if (!Config.HideCompassArrow)
                {
                    // Drawing the compass arrow pivoted at an offset in the +X direction about the intersection point
                    // and rotated in the direction of the intersection point to center of the peer.
                    var arrowPos = compassPos + new Vector2((float)Math.Cos(arrowAngle), (float)Math.Sin(arrowAngle))
                        * (36 * Game1.options.uiScale);
                    Utility.DrawUiSprite(e.SpriteBatch, ArrowTexture, arrowPos, 0.75f, arrowAngle + MathHelper.PiOver2, Config.Alpha / 100.0f);   
                }
            }
        }
    }
}