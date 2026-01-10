using UnityEngine;
using System;

public class  LevelManager : MonoBehaviour, IGameStateListener
{
    public static LevelManager instance;
    [Header(" Data ")]
    [SerializeField] private Level[] levels;
    private const string levelKey = "LevelReached";
    private int levelIndex;
    public Item[] Items => currentLevel.GetItems();
    
    [Header(" Settings ")]
    private Level currentLevel;
    
    [Header(" Action ")]
    public static Action<Level> levelSpawned;
    


    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
        
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
