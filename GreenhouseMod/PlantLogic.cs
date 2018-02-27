using Harmony;
using Plukit.Base;
using Staxel;
using Staxel.Behavior;
using Staxel.Farming;
using Staxel.Items;
using Staxel.Logic;
using Staxel.Notifications;
using Staxel.Player;
using Staxel.Tiles;
using System.Collections.Generic;
using System.Reflection;

namespace GreenhouseMod
{
	public class PlantLogic
	{
		private const string GreenhouseTotemCode = "mods.barrybadpak.totem.Greenhouse";
		private static readonly FieldInfo TotemScanCentre = AccessTools.Field(typeof(Totem), "_scanCentre");
		private static readonly FieldInfo TotemRegion = AccessTools.Field(typeof(Totem), "_region");
		private static readonly FieldInfo VillageTotemsByCode = AccessTools.Field(typeof(Village), "_totemsByCode");
		private static readonly MethodBase FarmDatabaseTileUpdated = AccessTools.Method(typeof(FarmingDatabase), "TileUpdated");
		private static readonly MethodBase FarmDatabaseChangeTile = AccessTools.Method(typeof(FarmingDatabase), "ChangeTile");
		private static readonly MethodBase FarmDatabaseMakeUnwateredMaterial = AccessTools.Method(typeof(FarmingDatabase), "MakeUnwateredMaterial");
		private static readonly MethodBase FarmDatabaseMakeWateredMaterial = AccessTools.Method(typeof(FarmingDatabase), "MakeWateredMaterial");
		private static readonly MethodBase FarmDatabaseIsMaterialWaterable = AccessTools.Method(typeof(FarmingDatabase), "IsMaterialWaterable");

		/// <summary>
		/// Check the plantability in case it previously failed with code *.WrongSeason*
		/// </summary>
		/// <param name="entity"></param>
		/// <param name="cursor"></param>
		/// <param name="tile"></param>
		/// <param name="facade"></param>
		/// <returns></returns>
		public static void CheckPlantability(ref bool __result, Entity entity, Vector3I cursor, Tile tile, EntityUniverseFacade facade, ref string reason, ref NotificationParams parameters)
		{
			List<Totem> totems;
			if (PlantLogic.GetValidTotemsOfType(PlantLogic.GreenhouseTotemCode, out totems))
			{
				// If the original result was not false and not because of WrongSeason
				if (__result != false && !reason.Contains("WrongSeason"))
				{
					return;
				}

				Totem totem;
				if(PlantLogic.IsInTotemsRegion(totems, cursor, out totem))
				{
					__result = true;
					reason = "";
					parameters = NotificationParams.EmptyParams;
				}
			}
		}

		/// <summary>
		/// This method replaced the original CheckPlantGrowthStage
		/// This method is public to avoid Protected memory access to the arguments (needs to be same access level as original)
		/// </summary>
		/// <param name="entity"></param>
		/// <param name="facade"></param>
		public void CheckPlantGrowthStage(Entity entity, EntityUniverseFacade facade)
		{
			PlayerEntityLogic playerEntityLogic = entity.PlayerEntityLogic;
			Tile tile = default(Tile);
			PlantConfiguration config = default(PlantConfiguration);
			Vector3I cursor = default(Vector3I);
			Vector3I adjacent = default(Vector3I);
			Vector3I core = default(Vector3I);

			if (playerEntityLogic != null && playerEntityLogic.LookingAtTile(out cursor, out adjacent) && ((ItemFacade)facade).FindReadCompoundTileCore(cursor, TileAccessFlags.None, out core, out tile) && GameContext.PlantDatabase.TryGetByTile(tile, out config))
			{
				Season season = SeasonHelper.FromInt(facade.DayNightCycle().GetSeason());
				bool livesInThisSeason = config.LivesInSeason(season);

				Totem totem;
				List<Totem> totems;
				bool plantedInGreenhouse = (PlantLogic.GetValidTotemsOfType(PlantLogic.GreenhouseTotemCode, out totems) && PlantLogic.IsInTotemsRegion(totems, cursor, out totem));

				Notification notif;
				if (config.IsWitheredTile(tile))
				{
					notif = ((!livesInThisSeason && !plantedInGreenhouse) ? GameContext.NotificationDatabase.CreateNotificationFromCode("staxel.notifications.checkPlantGrowth.witheredSeason", entity.Step, NotificationParams.CreateFromTranslation(season.GetCode()), false) : GameContext.NotificationDatabase.CreateNotificationFromCode("staxel.notifications.checkPlantGrowth.withered", entity.Step, NotificationParams.EmptyParams, false));
				}
				else if (config.IsWiltedTile(tile))
				{
					notif = GameContext.NotificationDatabase.CreateNotificationFromCode("staxel.notifications.checkPlantGrowth.wilted", entity.Step, NotificationParams.EmptyParams, false);
				}
				else if (!livesInThisSeason && !plantedInGreenhouse)
				{
					notif = GameContext.NotificationDatabase.CreateNotificationFromCode("staxel.notifications.checkPlantGrowth.season", entity.Step, NotificationParams.CreateFromTranslation(season.GetCode()), false);
				}
				else
				{
					float percentage = config.GetGrowthPercentage(tile, config);
					string description = (!(percentage < 0.1f)) ? ((!(percentage < 1f)) ? "hintText.checkPlantGrowth.fruited" : "hintText.checkPlantGrowth.growing") : "hintText.checkPlantGrowth.seed";
					notif = GameContext.NotificationDatabase.CreateNotificationFromCode("staxel.notifications.checkPlantGrowth", entity.Step, NotificationParams.CreateFromTranslation(description), false);
				}
				playerEntityLogic.ShowNotification(notif);
			}
			entity.Logic.ActionFacade.NoNextAction();
		}

		/// <summary>
		/// DailyVisit before function
		/// </summary>
		/// <param name="plantBlob"></param>
		/// <param name="plantLocation"></param>
		/// <param name="plantTile"></param>
		/// <param name="soilLocation"></param>
		/// <param name="soilTile"></param>
		/// <param name="universe"></param>
		/// <param name="weatherWatered"></param>
		/// <returns></returns>
		public bool DailyVisit(Blob plantBlob, Vector3I plantLocation, Tile plantTile, Vector3I soilLocation, Tile soilTile, EntityUniverseFacade universe, bool weatherWatered)
		{
			// Return true in case it concerns a greenhouse plant

			int day = universe.DayNightCycle().Day;
			int season = universe.DayNightCycle().GetSeason();
			bool watered = GameContext.FarmingDatabase.IsWateredMaterial(soilTile.Configuration);
			if (GameContext.PlantDatabase.IsGrowable(plantTile))
			{
				PlantConfiguration plantConfiguration = GameContext.PlantDatabase.GetByTile(plantTile.Configuration);
				DeterministicRandom rnd = GameContext.RandomSource;
				bool requiresWatering = GameContext.PlantDatabase.RequiresWatering(plantTile);
				long lastChangedDay = plantBlob.GetLong("day", -1L);
				int prevSeason2 = (int)plantBlob.GetLong("season", -1L);
				List<Totem> totems;
				Totem totem;
				bool plantedInGreenhouse = (PlantLogic.GetValidTotemsOfType(PlantLogic.GreenhouseTotemCode, out totems) && PlantLogic.IsInTotemsRegion(totems, plantLocation, out totem));

				if(plantConfiguration.LivesInSeason(SeasonHelper.FromInt(season)) 
					|| !plantConfiguration.LivesInSeason(SeasonHelper.FromInt(season)) && !plantedInGreenhouse)
				{
					return true;
				}

				Logger.WriteLine("Custom plant DailyVisit");

				if (prevSeason2 == -1)
				{
					prevSeason2 = season;
				}

				if (lastChangedDay == -1 || lastChangedDay > day)
				{
					lastChangedDay = day - 1;
					PlantLogic.FarmDatabaseTileUpdated.Invoke(GameContext.FarmingDatabase, new object[] { plantLocation, lastChangedDay, plantConfiguration.GatherableIsHarvestable, season });
				}

				Vector2I window2 = default(Vector2I);
				Vector2I window3 = default(Vector2I);
				Logger.WriteLine("Plant: " + plantConfiguration.Code);
				Logger.WriteLine("Watered: " + watered.ToString() + " | RequiresWatering: " + requiresWatering.ToString() + " | CanWilt: "+ plantConfiguration.CanWilt(plantTile, out window2).ToString() + " | CanWither: "+ plantConfiguration.CanWither(plantTile, out window3).ToString());
			
				long totalDays = day - lastChangedDay;
				int growthDays = 0;
				int daysPassed = 1;
				for (int i = 0; i < totalDays; i++)
				{
					Vector2I window = default(Vector2I);
					if (!watered && requiresWatering && plantConfiguration.CanWilt(plantTile, out window))
					{
						Logger.WriteLine("Wilting result:" + ((bool)PlantLogic.FarmDatabaseChangeTile.Invoke(GameContext.FarmingDatabase, new object[] { plantLocation, plantConfiguration.MakeWiltedTile(plantTile), universe, plantConfiguration.GatherableIsHarvestable, null, TileAccessFlags.IgnoreEntities })).ToString());
						int transitionPeriod3 = rnd.Next(window.X, window.Y);
						if (transitionPeriod3 <= daysPassed - growthDays
							&& (bool) PlantLogic.FarmDatabaseChangeTile.Invoke(GameContext.FarmingDatabase, new object[] { plantLocation, plantConfiguration.MakeWiltedTile(plantTile), universe, plantConfiguration.GatherableIsHarvestable, null, TileAccessFlags.IgnoreEntities  }))
						{
							Logger.WriteLine("Wilting greenhouse plant");
							break;
						}
					}
					if (!watered && requiresWatering && plantConfiguration.CanWither(plantTile, out window))
					{
						Logger.WriteLine("Withering result: " + ((bool)PlantLogic.FarmDatabaseChangeTile.Invoke(GameContext.FarmingDatabase, new object[] { plantLocation, plantConfiguration.MakeWitheredTile(plantTile), universe, plantConfiguration.GatherableIsHarvestable, null, TileAccessFlags.IgnoreEntities })).ToString());
						int transitionPeriod2 = rnd.Next(window.X, window.Y);
						if (transitionPeriod2 <= daysPassed - growthDays 
							&& (bool) PlantLogic.FarmDatabaseChangeTile.Invoke(GameContext.FarmingDatabase, new object[] { plantLocation, plantConfiguration.MakeWitheredTile(plantTile), universe, plantConfiguration.GatherableIsHarvestable, null, TileAccessFlags.IgnoreEntities }))
						{
							Logger.WriteLine("Withering greenhouse plant");
							break;
						}
					}
					if (!watered && requiresWatering && !plantConfiguration.CanWilt(plantTile, out window) && !plantConfiguration.CanWither(plantTile, out window))
					{
						Logger.WriteLine("Set as harvestable");
						PlantLogic.FarmDatabaseTileUpdated.Invoke(GameContext.FarmingDatabase, new object[] { plantLocation, day, plantConfiguration.GatherableIsHarvestable, season });
						break;
					}
					if ((watered || !requiresWatering || (plantConfiguration.GatherableIsHarvestable && rnd.NextBool())) && plantConfiguration.CanGrow(plantTile, SeasonHelper.FromInt(season), out window))
					{
						int transitionPeriod = rnd.Next(window.X, window.Y);
						if (transitionPeriod <= daysPassed - growthDays
							&& (bool)PlantLogic.FarmDatabaseChangeTile.Invoke(GameContext.FarmingDatabase, new object[] { plantLocation, plantConfiguration.MakeGrowTile(plantTile, rnd), universe, plantConfiguration.GatherableIsHarvestable, null, TileAccessFlags.IgnoreEntities }))
						{
							growthDays += transitionPeriod;
							if (!universe.ReadTile(plantLocation, TileAccessFlags.None, out plantTile))
							{
								return true;
							}
						}
					}

					daysPassed++;
				}


				if (!weatherWatered && watered)
				{
					Tile unwateredTile = (Tile)PlantLogic.FarmDatabaseMakeUnwateredMaterial.Invoke(GameContext.FarmingDatabase, new object[] { soilTile });
					PlantLogic.FarmDatabaseChangeTile.Invoke(null, new object[] { soilLocation, unwateredTile, universe, false, null, TileAccessFlags.IgnoreEntities });
				}

				Tile tempPlant = default(Tile);
				PlantConfiguration config = default(PlantConfiguration);
				bool isMaterialWaterable = (bool)PlantLogic.FarmDatabaseIsMaterialWaterable.Invoke(GameContext.FarmingDatabase, new object[] { soilTile.Configuration });
				if (weatherWatered && !watered && isMaterialWaterable && universe.ReadTile(plantLocation, TileAccessFlags.None, out tempPlant) && GameContext.PlantDatabase.TryGetByTile(tempPlant, out config))
				{
					Tile wateredTile = (Tile)PlantLogic.FarmDatabaseMakeWateredMaterial.Invoke(GameContext.FarmingDatabase, new object[] { soilTile });
					PlantLogic.FarmDatabaseChangeTile.Invoke(null, new object[] { soilLocation, wateredTile, universe, false, null, TileAccessFlags.IgnoreEntities });
				}

				return false;
			}

			return true;
		}

		/// <summary>
		/// Retrieve a list of all valid totems by code
		/// </summary>
		/// <param name="code"></param>
		/// <param name="totems"></param>
		/// <returns></returns>
		private static bool GetValidTotemsOfType(string code, out List<Totem> totems)
		{
			totems = new List<Totem>();
			Dictionary<string, List<Totem>> VillageTotemsByCode = (Dictionary<string, List<Totem>>)PlantLogic.VillageTotemsByCode.GetValue(ServerContext.VillageDirector.Village);
			List<Totem> TotemsByCode = default(List<Totem>);

			if (!VillageTotemsByCode.TryGetValue(code, out TotemsByCode))
			{
				return false;
			}

			foreach (Totem item in TotemsByCode)
			{
				if (item.Valid())
				{

					totems.Add(item);
				}
			}

			return totems != null;
		}

		/// <summary>
		/// Check if the given position lies within any of the provided totem regions
		/// </summary>
		/// <param name="totems"></param>
		/// <param name="position"></param>
		/// <param name="totem"></param>
		/// <returns></returns>
		private static bool IsInTotemsRegion(List<Totem> totems, Vector3I position, out Totem totem)
		{
			totem = null;
			foreach (Totem item in totems)
			{

				Vector3I TotemRegion = (Vector3I)PlantLogic.TotemRegion.GetValue(item);
				Vector3I TotemScanCentre = (Vector3I)PlantLogic.TotemScanCentre.GetValue(item);

				if (TotemRegion != null && TotemScanCentre != null
					&& !(position.X < (double)(TotemScanCentre.X - TotemRegion.X))
					&& !(position.X > (double)(TotemScanCentre.X + TotemRegion.X))
					&& !(position.Y < (double)(TotemScanCentre.Y - TotemRegion.Y))
					&& !(position.Y > (double)(TotemScanCentre.Y + TotemRegion.Y))
					&& !(position.Z < (double)(TotemScanCentre.Z - TotemRegion.Z))
					&& !(position.Z > (double)(TotemScanCentre.Z + TotemRegion.Z)))
				{
					totem = item;
					return true;
				}
			}

			return false;
		}
	}
}