using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Utility;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;

namespace MedicalKitsMod
{
    public class MedicalKitsMain : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<MedicalKitsMain>();

            mod.IsReady = true;
        }

        private void Start()
        {
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(MedicalKitItem.templateIndex, ItemGroups.None, typeof(MedicalKitItem));

            PlayerActivate.OnLootSpawned += MedicalKitsInStores_OnLootSpawned;
            EnemyDeath.OnEnemyDeath += MedicalKitOnHealer_OnEnemyDeath;
        }

        // When an enemy Healer dies, a medical kit has a small chance (influenced slightly by luck) to spawn on the Healer.
        // If the kit spawns, it randomly selects how many uses the kit has (1 - 5).
        // The number of uses is kept track with the value of the item.
        // If the kit has been used, it updates the kit's name and adjusts the value of the kit.
        private void MedicalKitOnHealer_OnEnemyDeath(object sender, EventArgs e)
        {
            EnemyDeath enemyDeath = sender as EnemyDeath;

            if (enemyDeath != null)
            {
                DaggerfallEntityBehaviour entityBehaviour = enemyDeath.GetComponent<DaggerfallEntityBehaviour>();

                if (entityBehaviour != null)
                {
                    EnemyEntity enemyEntity = entityBehaviour.Entity as EnemyEntity;

                    if (enemyEntity != null) {
                        if (enemyEntity.MobileEnemy.ID == (int) MobileTypes.Healer)
                        {
                            int luckMod = (int) Mathf.Floor((GameManager.Instance.PlayerEntity.Stats.LiveLuck - 50f) / 10); ;

                            if (Dice100.SuccessRoll(15 + luckMod))
                            {
                                int uses = UnityEngine.Random.Range(1, 6);
                                DaggerfallUnityItem medicalKit = ItemBuilder.CreateItem(ItemGroups.None, MedicalKitItem.templateIndex);

                                if (uses < 5)
                                {
                                    medicalKit.value = 100 * uses;
                                    ((MedicalKitItem)medicalKit).updateName();
                                }

                                entityBehaviour.CorpseLootContainer.Items.AddItem(medicalKit);
                            }
                        }
                    }
                }
            }
        }

        // When the player clicks on a shop shelf, checks the building type.
        // If a general store, it has a lower chance of spawning and will only spawn one kit.
        // If an alchemist store, it has a higher chance of spawning and may spawning multiple kits (1 - 3).
        private void MedicalKitsInStores_OnLootSpawned(object sender, ContainerLootSpawnedEventArgs e)
        {
            DaggerfallInterior interior = GameManager.Instance.PlayerEnterExit.Interior;

            if (interior != null && e.ContainerType == LootContainerTypes.ShopShelves)
            {
                if (interior.BuildingData.BuildingType == DFLocation.BuildingTypes.GeneralStore)
                {
                    if (Dice100.SuccessRoll(3 * interior.BuildingData.Quality))
                    {
                        DaggerfallUnityItem medicalKit = ItemBuilder.CreateItem(ItemGroups.None, MedicalKitItem.templateIndex);
                        e.Loot.AddItem(medicalKit);
                    }
                }

                else if (interior.BuildingData.BuildingType == DFLocation.BuildingTypes.Alchemist)
                {
                    if (Dice100.SuccessRoll(4 * interior.BuildingData.Quality))
                    {
                        int numOfKits = UnityEngine.Random.Range(1, 4);

                        while (numOfKits > 0)
                        {
                            DaggerfallUnityItem medicalKit = ItemBuilder.CreateItem(ItemGroups.None, MedicalKitItem.templateIndex);
                            e.Loot.AddItem(medicalKit);
                            numOfKits--;
                        }
                    }
                }
            }
        }
    }
}
