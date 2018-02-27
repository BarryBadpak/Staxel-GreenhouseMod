using Harmony;
using Plukit.Base;
using Staxel.Farming;
using Staxel.Logic;
using Staxel.Tiles;

namespace GreenhouseMod.Patches.FarmingDatabaseNS
{
	[HarmonyPatch(typeof(FarmingDatabase), "DailyVisit")]
	class DailyVisitPatch
	{

		// <summary>
		// Patch the daily visit before, we use almost identical logic but only if the plant is out of season and it is within a greenhouse
		// otherwise we let the default code handle it
		// </summary>
		// <param name = "plantBlob" ></ param >
		// < param name="plantLocation"></param>
		// <param name = "plantTile" ></ param >
		// < param name="soilLocation"></param>
		// <param name = "soilTile" ></ param >
		// < param name="universe"></param>
		// <param name = "weatherWatered" ></ param >
		// < returns ></ returns >
		[HarmonyPrefix]
		public static bool BeforeDailyVisit(Blob plantBlob, Vector3I plantLocation, Tile plantTile, Vector3I soilLocation, Tile soilTile, EntityUniverseFacade universe, bool weatherWatered)
		{
			return GreenhouseModManager.Instance.PlantLogic.DailyVisit(plantBlob, plantLocation, plantTile, soilLocation, soilTile, universe, weatherWatered);
		}
	}
}
