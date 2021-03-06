﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HoMM;
using Homm.Client.Helpers;
using HoMM.ClientClasses;
using HommFinder;

namespace Homm.Client.Actions
{
	public class ActionManager
	{
		public HommSensorData SensorData { get; set; }
		public  HommClient Client { get; private set; }
		public List<Cell> Map { get; private set; } 
		public Cell CurrentCell { get; private set; }
		private Finder _finder;
		public MapType MapType { get; private set; }
		public MapObjectData EnemyRespawn { get; private set; }

        public ActionManager(HommClient client, HommSensorData sensorData)
		{
			Client = client;
			SensorData = sensorData;
			
			var startCell  = sensorData.Location.CreateCell();

			EnemyRespawn =
				startCell.SameLocation(new Cell(0, 0)) ?
				sensorData.Map.Objects.SingleOrDefault(o => o.Location.X == 13 && o.Location.Y == 13) :
				sensorData.Map.Objects.SingleOrDefault(o => o.Location.X == 0 && o.Location.Y == 0);
			MapType = MapType.Single;

			if (sensorData.Map.Objects.Count < sensorData.Map.Height * sensorData.Map.Width)
			{
				MapType = MapType.DualHard;
			}
			else if (EnemyRespawn.Hero != null)
			{
				MapType = MapType.Dual;
			}
			
			Map = new List<Cell>();		
		}

		//TODO:Need to call this function every day if playing vs player, or you don't see whole map
		public void UpdateMap()
		{
			Map.Clear();
			Map = SensorData.Map.Objects.Select(item => item.ToCell(SensorData.MyArmy)).ToList();
			CurrentCell = SensorData.Location.CreateCell();

			_finder = new Finder(Map,CurrentCell);
		}
	
		public List<Direction> MoveToCell(Cell cell)
		{
			UpdateMap();
			return Converter.ConvertCellPathToDirection(_finder.GetMovesStraightToCell(cell));
		}

		public List<Direction> MoveToCell(MapObjectData mapObj)
		{
			return MoveToCell(mapObj.ToCell());
		}
		//TODO:: implement 3 methods for different types of map(single, dual, dualHard)
		//TODO: change signature of this method
		public void Play()
		{
			UpdateMap();
		    workingWithMines();
		    workingWithDwellings();
		}

	    private void workingWithMines()
	    {
	        var path = new List<Cell>();

	        var availableMines = searchAvailableMines(_finder._cells);
	        for (int i = 0; i < availableMines.Count; i++)
	        {
	            path.AddRange(_finder.GetSmartPath(SensorData.Location.CreateCell(), (availableMines[i])));
	        }

	        if (path.Count != 0)
	        {
	            move(path);
	        }
	    }

	    private void workingWithDwellings()
	    {
            var path = new List<Cell>();
	        var dwelling = getAvailableDwelling(_finder._cells);

		    if (dwelling == null) return;
		    switch (dwelling.CellType.SubCellType)
		    {
			    case SubCellType.DwellingCavalry:
			    {
				    path = useDwelling(dwelling, UnitType.Cavalry, Resource.Ebony);
				    if (path.Count != 0)
				    {
					    move(path);
					    SensorData = Client.HireUnits(getAmountOfUnitsToBuy(SubCellType.DwellingCavalry, dwelling));
				    }
				    break;
			    }

			    case SubCellType.DwellingInfantry:
			    {
				    path = useDwelling(dwelling, UnitType.Infantry, Resource.Iron);
				    if (path.Count != 0)
				    {
					    move(path);
					    SensorData = Client.HireUnits(getAmountOfUnitsToBuy(SubCellType.DwellingInfantry, dwelling));
				    }
				    break;
			    }

			    case SubCellType.DwellingRanged:
			    {
				    path = useDwelling(dwelling, UnitType.Ranged, Resource.Glass);
				    if (path.Count != 0)
				    {
					    move(path);
					    SensorData = Client.HireUnits(getAmountOfUnitsToBuy(SubCellType.DwellingRanged, dwelling));
				    }
				    break;
			    }
			    case SubCellType.DwellingMilitia:
			    {
				    path = useDwelling(dwelling, UnitType.Militia, Resource.Gold);
				    if (path.Count != 0)
				    {
					    move(path);
					    SensorData = Client.HireUnits(getAmountOfUnitsToBuy(SubCellType.DwellingMilitia, dwelling));
				    }
				    break;
			    }
		    }
	    }

	    private Cell getAvailableDwelling(List<Cell> finderCells)
        {
	        var availableDwellings = finderCells.Where(i => (i.CellType.MainType == MainCellType.Dwelling)
                           && !i.Value.Equals(Single.MaxValue) && (i.ResourcesValue > 0)).OrderByDescending(i => i.Value).ToList();

			if (availableDwellings.Count != 0)
				return availableDwellings.FirstOrDefault(i => (i.CellType.SubCellType != SubCellType.DwellingMilitia)) ??
							availableDwellings.FirstOrDefault();
	        return null;
        }

		private List<Cell> searchAvailableMines(List<Cell> finderCells)
        {
            return finderCells.Where(i => (i.CellType.MainType == MainCellType.Mine)
                           && !i.Value.Equals(Single.MaxValue)).ToList();
        }

        private List<Cell> useDwelling(Cell dwellingCheck, UnitType unitType, Resource resource)
        {
            var path = new List<Cell>();
            var missingTreasury = existTreasuryForDwelling(dwellingCheck, unitType, resource);
            path = missingTreasury.Count == 0 ? _finder.GetMovesStraightToCell(dwellingCheck) : 
				findResourcesForDwelling(missingTreasury, dwellingCheck, resource, _finder._cells);
            return path;
        }

        private Dictionary<Resource, int> existTreasuryForDwelling(Cell dwellingCheck, UnitType unitType, Resource resource = new Resource())
        {

            var missingResources = new Dictionary<Resource, int>();

            if (dwellingCheck.CellType.SubCellType == SubCellType.DwellingMilitia)
            {
                if (SensorData.MyTreasury[Resource.Gold] >= UnitsConstants.Current.UnitCost[UnitType.Militia][Resource.Gold])
                {
                    return new Dictionary<Resource, int>();
                }
                missingResources.Add(Resource.Gold,
                    UnitsConstants.Current.UnitCost[UnitType.Militia][Resource.Gold] - SensorData.MyTreasury[Resource.Gold]);
            }
            else
            {
                if (SensorData.MyTreasury[Resource.Gold] >= UnitsConstants.Current.UnitCost[unitType][Resource.Gold] &&
					SensorData.MyTreasury[resource] >= UnitsConstants.Current.UnitCost[unitType][resource])
                {
                    return new Dictionary<Resource, int>();
                }

                missingResources.Add(Resource.Gold,
                    UnitsConstants.Current.UnitCost[unitType][Resource.Gold] - SensorData.MyTreasury[Resource.Gold]);

                missingResources.Add(resource,
                    UnitsConstants.Current.UnitCost[unitType][resource] - SensorData.MyTreasury[resource]);
            }

            return missingResources;
        }

        private List<Cell> findResourcesForDwelling(Dictionary<Resource, int> missingTreasury, 
            Cell dwelling, Resource resource, List<Cell> finderCells)
        {
            var subCellType = new SubCellType();
	        switch (resource)
	        {
			    case Resource.Ebony:
					subCellType = SubCellType.ResourceEbony;
					break;
				case Resource.Iron:
					subCellType = SubCellType.ResourceIron;
					break;
				case Resource.Glass:
					subCellType = SubCellType.ResourceGlass;
					break;
			}

            var resultCellsList = new List<Cell>();
			var foundedCells = finderCells.Where(o => (o.CellType.SubCellType == SubCellType.ResourceGold && o.ResourcesValue > 0) 
						&& !o.Value.Equals(Single.MaxValue) &&!resultCellsList.Contains(o)).OrderBy(o => o.Value).ToList();

	        for (int i = 0; i < foundedCells.Count && missingTreasury[Resource.Gold] > 0; i++)
	        {
				var cell = foundedCells.ElementAt(i);
		        resultCellsList.Add(cell);
		        missingTreasury[Resource.Gold] = missingTreasury[Resource.Gold] - cell.ResourcesValue;
			}

			if (resource != Resource.Gold)
			{
				foundedCells = finderCells.Where(o => (o.CellType.SubCellType == subCellType || o.ResourcesValue > 0)
						&& !o.Value.Equals(Single.MaxValue) && !resultCellsList.Contains(o)).OrderBy(o => o.Value).ToList();

				for (int i = 0; i < foundedCells.Count && missingTreasury[resource] > 0; i++)
				{
					var cell = foundedCells.ElementAt(i);
					resultCellsList.Add(cell);
					missingTreasury[resource] = missingTreasury[resource] - cell.ResourcesValue;
				}
			}

			var cellPath = new List<Cell>();
	        if (resultCellsList.Count <= 0) return cellPath;
	        cellPath.AddRange(_finder.GetSmartPath(SensorData.Location.CreateCell(), resultCellsList[0]));
               
	        for (int y = 1; y < resultCellsList.Count; y++)
	        {
		        var finderNew = new Finder(finderCells, resultCellsList[y]);
		        cellPath.AddRange(finderNew.GetSmartPath(resultCellsList[y - 1], resultCellsList[y]));
	        }
	        var finderToEnd = new Finder(finderCells, resultCellsList[resultCellsList.Count - 1]);
	        cellPath.AddRange(finderToEnd.GetSmartPath(resultCellsList[resultCellsList.Count - 1], dwelling));
	        return cellPath;
        }

        private int getAmountOfUnitsToBuy(SubCellType subCellType, Cell dwellingCheck)
		{
			if (subCellType == SubCellType.DwellingMilitia)
			{
			    var amountOfUnitsToBuy = SensorData.MyTreasury[Resource.Gold] / UnitsConstants.Current.UnitCost[UnitType.Militia][Resource.Gold];
			    return dwellingCheck.ResourcesValue >= amountOfUnitsToBuy ? amountOfUnitsToBuy : dwellingCheck.ResourcesValue;
			}

			if (subCellType == SubCellType.DwellingCavalry)
			{
				var maxAmountGold = SensorData.MyTreasury[Resource.Gold] / UnitsConstants.Current.UnitCost[UnitType.Cavalry][Resource.Gold];
				var maxAmountEbony = SensorData.MyTreasury[Resource.Ebony] / UnitsConstants.Current.UnitCost[UnitType.Cavalry][Resource.Ebony];
			    var amountOfUnitsToBuy = maxAmountGold < maxAmountEbony ? maxAmountGold : maxAmountEbony;

				return dwellingCheck.ResourcesValue >= amountOfUnitsToBuy ? amountOfUnitsToBuy : dwellingCheck.ResourcesValue;
			}

			if (subCellType == SubCellType.DwellingInfantry)
			{
				var maxAmountGold = SensorData.MyTreasury[Resource.Gold] / UnitsConstants.Current.UnitCost[UnitType.Infantry][Resource.Gold];
				var maxAmountIron = SensorData.MyTreasury[Resource.Iron] / UnitsConstants.Current.UnitCost[UnitType.Infantry][Resource.Iron];
			    var amountOfUnitsToBuy = maxAmountGold < maxAmountIron ? maxAmountGold : maxAmountIron;

				return dwellingCheck.ResourcesValue >= amountOfUnitsToBuy ? amountOfUnitsToBuy : dwellingCheck.ResourcesValue;

			}

			if (subCellType == SubCellType.DwellingRanged)
			{
				var maxAmountGold = SensorData.MyTreasury[Resource.Gold] / UnitsConstants.Current.UnitCost[UnitType.Ranged][Resource.Gold];
				var maxAmountGlass = SensorData.MyTreasury[Resource.Glass] / UnitsConstants.Current.UnitCost[UnitType.Ranged][Resource.Glass];
			    var amountOfUnitsToBuy = maxAmountGold < maxAmountGlass ? maxAmountGold : maxAmountGlass;

				return dwellingCheck.ResourcesValue >= amountOfUnitsToBuy ? amountOfUnitsToBuy : dwellingCheck.ResourcesValue;

			}

			return 0;
		}

		private void move(List<Cell> path)
		{
			if (path.Count == 0) return;
			var steps = Converter.ConvertCellPathToDirection(path);
			for (var index = 0; index < steps.Count; index++)
			{
				SensorData = Client.Move(steps[index]);
			}
		}
	}

	public enum MapType
	{
		Single,
		Dual,
		//mode without open map
		DualHard
	}
}
