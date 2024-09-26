using p5r.code.multiplayerclient.Components;
using p5r.code.multiplayerclient.Configuration;
using p5r.code.multiplayerclient.Template;
using p5r.code.multiplayerclient.Utility;
using p5rpc.lib.interfaces;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
namespace p5r.code.multiplayerclient
{
    /// <summary>
    /// Your mod logic goes here.
    /// </summary>
    public class Mod : ModBase // <= Do not Remove.
    {
        /// <summary>
        /// Provides access to the mod loader API.
        /// </summary>
        private readonly IModLoader _modLoader;

        /// <summary>
        /// Provides access to the Reloaded.Hooks API.
        /// </summary>
        /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
        private readonly IReloadedHooks? _hooks;

        /// <summary>
        /// Provides access to the Reloaded logger.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Entry point into the mod, instance that created this class.
        /// </summary>
        private readonly IMod _owner;

        /// <summary>
        /// Provides access to this mod's configuration.
        /// </summary>
        private Config _configuration;

        /// <summary>
        /// The configuration of the currently executing mod.
        /// </summary>
        private readonly IModConfig _modConfig;


        private NpcManager _npcManager;
        private Multiplayer _multiplayer;
        public Mod(ModContext context)
        {
            _modLoader = context.ModLoader;
            _hooks = context.Hooks;
            _logger = context.Logger;
            _owner = context.Owner;
            _configuration = context.Configuration;
            _modConfig = context.ModConfig;

            // For more information about this template, please see
            // https://reloaded-project.github.io/Reloaded-II/ModTemplate/

            // If you want to implement e.g. unload support in your mod,
            // and some other neat features, override the methods in ModBase.

            // TODO: Implement some mod logic
            Utils.Initialise(_logger, _configuration, _modLoader );
            var p5rLibController = _modLoader.GetController<IP5RLib>();
            if (p5rLibController == null || !p5rLibController.TryGetTarget(out IP5RLib p5rLib))
            {
                // Tell the user that you couldn't access inputhook so stuff won't work
                _logger.WriteLine("Unabled to get P5R Lib! UH OH");
                return;
            }
            
            _multiplayer = new Multiplayer(p5rLib, _logger, _hooks, _configuration);
            _multiplayer.Connect(_configuration.ServerIpAddress, _configuration.ServerPort);

            /*IP5RLib _p5rLib = p5rLib;
            int fieldMajor = _p5rLib.FlowCaller.FLD_GET_MAJOR();
            int fieldMinor = _p5rLib.FlowCaller.FLD_GET_MINOR();
            _logger.WriteLine($"The current field is {fieldMajor}_{fieldMinor}");*/
        }
        
        #region Standard Overrides
        public override void ConfigurationUpdated(Config configuration)
        {
            // Apply settings from configuration.
            // ... your code here.
            _configuration = configuration;
            _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
        }
        #endregion

        #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Mod() { }
#pragma warning restore CS8618
        #endregion
    }
}