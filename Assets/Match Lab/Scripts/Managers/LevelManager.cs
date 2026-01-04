using UnityEngine;
using System;

public class LevelManager : MonoBehaviour, IGameStateListener
{
    [Header(" Levels ")]
    [SerializeField] private Level[] levels;
    private const string levelKey = "LevelReached";
    private int levelIndex;
    
    [Header(" Settings ")]
    private Level currentLevel;
    
    [Header(" Action ")]
    public static Action<Level> levelSpawned;
    


    private void Awake()
    {
        LoadData();
    }

    private void SpawnLevel()
    {
        transform.Clear();

        if (levels.Length <= 0)
        {
            Debug.LogError("No levels found");
            return;
        }
        
        int validatedIdx = levelIndex % levels.Length;
        currentLevel = Instantiate(levels[validatedIdx], transform);
        
        levelSpawned?.Invoke(currentLevel);
        
    }

    private void LoadData()
    {
        levelIndex = PlayerPrefs.GetInt(levelKey);
    }

    private void SaveData()
    {
        PlayerPrefs.SetInt(levelKey, levelIndex);
    }

    public void GameStateChangedCallback(EGameState gameState)
    {
        if(gameState == EGameState.GAME)
            SpawnLevel();
        else if (gameState == EGameState.LEVELCOMPLETE)
        {
            levelIndex++;
            SaveData(); 
        }

        
    }
}
