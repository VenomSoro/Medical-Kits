using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MedicalKitsMod
{
    public class MedicalKitItem : DaggerfallUnityItem
    {
        public const int templateIndex = 2106;

        public MedicalKitItem() : base(ItemGroups.MiscItems, templateIndex)
        {

        }

        public override ItemData_v1 GetSaveData()
        {
            ItemData_v1 data = base.GetSaveData();
            data.className = typeof(MedicalKitItem).ToString();
            return data;
        }

        public override bool UseItem(ItemCollection collection)
        {
            // If enemies are nearby, stop usage and message player.
            if (GameManager.Instance.AreEnemiesNearby())
            {
                DaggerfallUI.MessageBox("Using this now would leave you vulnerable to the enemies nearby.");
                return true;
            }

            PlayerEntity player = GameManager.Instance.PlayerEntity;

            // If fatigue is too low, stop usage and message player.
            if (player.CurrentFatigue <= 40 * DaggerfallEntity.FatigueMultiplier)
            {
                DaggerfallUI.MessageBox("Using this now would require too much energy.");
                return true;
            }

            int medicalSkill = player.Skills.GetLiveSkillValue(DFCareer.Skills.Medical);

            // If the player is at full health and can't cure diseases or poisons due to skill below 50, stop usage and message player.
            if (medicalSkill < 50 && player.CurrentHealth == player.MaxHealth)
            {
                DaggerfallUI.MessageBox("You are currently healthy, it would be a waste to use this.");
                return true;
            }

            // Get num of diseases and poisons currently ailing player
            int diseaseCount = GameManager.Instance.PlayerEffectManager.DiseaseCount;
            int poisonCount = GameManager.Instance.PlayerEffectManager.PoisonCount;


            // If player is at full health and can cure diseases and poisons, but is not currently diseased or poisoned, stop usage and message player.
            if (medicalSkill >= 50 && player.CurrentHealth == player.MaxHealth && diseaseCount == 0 && poisonCount == 0)
            {
                DaggerfallUI.MessageBox("You are currently healthy, it would be a waste to use this.");
                return true;
            }

            // Getting stat mods (-5 to +5 unless stats go beyond 100)
            int intelligenceMod = (int) Mathf.Floor((player.Stats.LiveIntelligence - 50f) / 10);
            int agilityMod = (int) Mathf.Floor((player.Stats.LiveAgility - 50f) / 10); ;
            int speedMod = (int) Mathf.Floor((player.Stats.LiveSpeed - 50f) / 10); ;
            int luckMod = (int) Mathf.Floor((player.Stats.LiveLuck - 50f) / 10); ;


            // The amount of health restored is a percentage of player max health. 10% is restored at minimum.
            // Intelligence has a small influence, the rest is determined by medical skill.
            // about 50% of the player's health will be restored around a medical skill of 75.
            int healAmount = (int) Mathf.Round(((10 + intelligenceMod + (medicalSkill / MedicalKitsMain.medicalSkillHealthRestoredDivisor)) / 100f) * player.MaxHealth);
            //Debug.Log(healAmount + " healed by medical kit.");

            bool isConditionReduced = true;

            player.IncreaseHealth(healAmount);

            // Using a medical kit provides medical skill experience. 10 exp will always be given.
            // Extra exp is based on how much health was restored.
            // This means that as the medical skill increases and player health increases,
            // The exp gained will scale. Hopefully this will keep up with scaling requirements
            // as the medical skill can quickly become ridiculous even at 1.0x difficulty (500+ points).
            short medicalExp = (short)Mathf.Round(10f + (healAmount / MedicalKitsMain.healthRestoredExperienceDivisor));

            // Check for additional bonuses due to having good medical skill.
            if (medicalSkill >= 50)
            {
                // If diseased, the player will have a skill - 20 % chance of curing themselves of diseases.
                // If cured, provide additional exp bonus.
                // Intelligence and Luck have a small influence.
                if (diseaseCount > 0 && Dice100.SuccessRoll(medicalSkill + intelligenceMod + luckMod - 20))
                {
                    GameManager.Instance.PlayerEffectManager.CureAllDiseases();
                    medicalExp += 10;
                    DaggerfallUI.Instance.PopupMessage("You cured yourself of all diseases");
                }

                // If poisoned, the player will have a skill - 20 % chance of curing themselves of poisons.
                // If cured, provide additional exp bonus.
                // Intelligence and Luck have a small influence.
                if (poisonCount > 0 && Dice100.SuccessRoll(medicalSkill + intelligenceMod + luckMod - 20))
                {
                    GameManager.Instance.PlayerEffectManager.CureAllPoisons();
                    medicalExp += 10;
                    DaggerfallUI.Instance.PopupMessage("You cured yourself of all poisons");
                }

                // Once skill is 50+, the player has a skill - 40 % chance of not using up any uses of the medical kit.
                // This should result in highly skill medics getting more use out of medical kits not only due to
                // healing more, but being more efficient in their usage of supplies.
                // Intelligence and Luck have a small influence.
                if (Dice100.SuccessRoll(medicalSkill + intelligenceMod + luckMod - 40))
                {
                    isConditionReduced = false;
                }
            }

            // Reduces value if flag is true.
            // Unless, the player's skill is 50+, this will always be true.
            if (isConditionReduced)
            {
                value -= 100;

                if (value <= 0)
                {
                    // Once value is 0, remove the kit and message player.
                    // Value is the way to keep track of uses across saves.
                    DaggerfallUI.Instance.PopupMessage("The medical kit has been used completely");

                    // Ensures the item is removed from all potential collections.
                    // This includes player inventory, player wagon, and other item piles.
                    collection.RemoveItem(this);
                }
                else
                {
                    // If not used up, update the name to display remaining uses.
                    updateName();
                }
            }

            // Message player about using the medical kit.
            DaggerfallUI.MessageBox("You spend some time treating yourself with a medical kit.");

            // Decrease fatigue so that medical kit usage cannot be spammed.
            player.DecreaseFatigue(20, true);

            // Give medical skill exp.
            //Debug.Log(medicalExp + " medical exp gained.");
            player.TallySkill(DFCareer.Skills.Medical, medicalExp);

            // Raise the time due to using the medical kit. How much time spent is influenced primarily by medical skill.
            // Individuals with high skill (100 to 60) will take 10 to 18 minutes.
            // Individuals with medium skill (59 to 30) will take 18 to 24 minutes.
            // Individuals with low skill (29 to 5) will take 24 to 29 minutes.
            // Agility and Speed have a small influence on time spent.
            float timeSpent = 1200 * (1.5f - ((medicalSkill + agilityMod + speedMod) / 100f));
            if (timeSpent < 300)
            {
                // Ensures that if medical skill is magically enhanced beyond 100,
                // the time spent will not become negative (minimum of 5 minutes).
                timeSpent = 300;
            }

            DaggerfallUnity.Instance.WorldTime.Now.RaiseTime(timeSpent);

            return true;
        }

        // Updates the name of the medical kit based on how many uses it has remaining.
        public void updateName()
        {
            RenameItem("Medical Kit (" + value / 100 + " / 5)");
        }
    }
}