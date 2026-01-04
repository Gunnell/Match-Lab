using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    
    public static GameManager instance;
    private EGameState gameState;


    private void Awake()
    {
        if(instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    { 
        SetGameState(EGameState.MENU);
    }
  
    // Update is called once per frame
    void Update()
    {

    }

    public void SetGameState(EGameState gameState)
    {
        this.gameState = gameState;

       IEnumerable<IGameStateListener> gameStateListeners
            = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IGameStateListener>();

        foreach(IGameStateListener dependency in gameStateListeners)
            dependency.GameStateChangedCallback(gameState); 
        
    }

    public void StartGame()
    {
        SetGameState(EGameState.GAME); 
    }
}
