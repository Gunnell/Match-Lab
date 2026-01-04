using UnityEngine;

public class UIManager : MonoBehaviour, IGameStateListener
{
    
    [Header(" Panels ")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject levelCompletedPanel;
    [SerializeField] private GameObject gameOverPanel;

    public void GameStateChangedCallback(EGameState gameState)
    {
       /* menuPanel.SetActive(gameState == EGameState.MENU);
        menuPanel.SetActive(gameState == EGameState.GAME);
        menuPanel.SetActive(gameState == EGameState.LEVELCOMPLETE);
        menuPanel.SetActive(gameState == EGameState.GAMEOVER); */

    }




}
