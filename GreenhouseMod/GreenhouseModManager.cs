using GreenhouseMod.Patches.CheckPlantGrowthStageActionNS;
using Harmony;
using Plukit.Base;
using Staxel.EntityActions;
using Staxel.Farming;
using Staxel.Logic;
using Staxel.Notifications;
using Staxel.Tiles;
using Sunbeam;
using System;
using System.Reflection;

namespace GreenhouseMod
{
	public class GreenhouseModManager : SunbeamMod
	{
		public override string ModIdentifier => "GreenHouse";
		public static GreenhouseModManager Instance { get; private set; }

		public PlantLogic PlantLogic { get; private set; }

		public GreenhouseModManager()
		{
			GreenhouseModManager.Instance = this;
			this.PlantLogic = new PlantLogic();
		
			this.ApplyManualPatches();
		}

		/// <summary>
		/// We need to apply patches manually, since one of our patches targets a method with ByRef parameters
		/// and this does not work with annotations
		/// </summary>
		internal void ApplyManualPatches()
		{
			Type[] MethodTypes = new Type[] { typeof(Entity), typeof(Vector3I), typeof(Tile), typeof(EntityUniverseFacade), typeof(string).MakeByRefType(), typeof(NotificationParams).MakeByRefType() };
			MethodBase OriginalMethod = typeof(PlantConfiguration).GetMethod("CheckPlantability", MethodTypes);
			HarmonyMethod HarmonyMethodPatch = new HarmonyMethod(typeof(PlantLogic), "CheckPlantability");
			this.HarmonyInstance.Patch(OriginalMethod, null, HarmonyMethodPatch, null);
		}
	}
}
