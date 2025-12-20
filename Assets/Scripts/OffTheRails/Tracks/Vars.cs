using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OffTheRails.Tracks;

namespace OffTheRails.Tracks
{
    public class Vars : MonoBehaviour
    {
        //This script is used to store static variables that are used throughout the game
        public static int totalLevels = 100;
        public static float levelTimeStopwatch = 0;
        public static int remainingLives = 3;
        public static int numberOfCoins = 0;
        public static int totalCoinsCollected = 0;

        public static void ResetAllSettings()
        {
            levelTimeStopwatch = 0;
            remainingLives = 3;
            totalCoinsCollected = 0;
            numberOfCoins = 0;
        }
    }
}
