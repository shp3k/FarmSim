using System;
using System.Collections.Generic;

[Serializable]
public class PlayerProfileCloudData
{
    public string displayName = "Player";
    public string email = "";
    public long createdAt;
    public long lastLoginAt;
    public long lastSaveAt;
}

[Serializable]
public class PlayerProgressCloudData
{
    public int coins = 100;
    public int totalRunEarnings;
    public int totalPrestigeCount;
    public int prestigePoints;
    public TutorialHintsCloudData tutorialHints = new();
    public Dictionary<string, CropProgressCloudData> crops = new();
    public Dictionary<string, AnimalProgressCloudData> animals = new();
    public PrestigeUpgradesCloudData prestigeUpgrades = new();
}

[Serializable]
public class TutorialHintsCloudData
{
    public bool tabHintSeen;
    public bool shopHintSeen;
    public bool harvestHintSeen;
    public bool animalHintSeen;
    public bool prestigeHintSeen;
}

[Serializable]
public class CropProgressCloudData
{
    public int soldCount;
    public bool unlocked;
}

[Serializable]
public class AnimalProgressCloudData
{
    public int ownedCount;
    public int collectedProductCount;
    public bool unlocked;
}

[Serializable]
public class PrestigeUpgradesCloudData
{
    public int cropGrowthSpeed;
    public int cropProfit;
    public int animalProductionSpeed;
    public int animalProfit;
    public int startingCapital;
    public int cropDiscount;
    public int animalDiscount;
    public int farmerExperience;
    public int animalKeeperExperience;
    public int skillfulHarvest;
}

[Serializable]
public class RunStateCloudData
{
    public Dictionary<string, PlantedCropCloudData> plantedCrops = new();
    public Dictionary<string, SpawnedAnimalCloudData> spawnedAnimals = new();
}

[Serializable]
public class PlantedCropCloudData
{
    public string cropId = "";
    public float growthProgressSeconds;
    public int stage;
    public bool readyToHarvest;
}

[Serializable]
public class SpawnedAnimalCloudData
{
    public string animalId = "";
    public int spawnPointIndex;
    public float productionProgressSeconds;
    public bool productReady;
}

[Serializable]
public class PlayerCloudSaveData
{
    public PlayerProfileCloudData profile = new();
    public PlayerProgressCloudData progress = new();
    public RunStateCloudData runState = new();
}
