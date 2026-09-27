using UnityEngine;

public class UIManager : MonoBehaviour, IGameStateListener
{

    [Header(" Panels ")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject levelCompletedPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header(" Settings ")]
    [Tooltip("Keeps the game panel up after the win so the last goal card can play its Complete animation.")]
    [SerializeField] private float levelCompletePanelDelay = 2.5f;

    public void GameStateChangedCallback(EGameState gameState)
    {
        // The state switches right away (input and timer stop), only the
        // panel swap waits. Goal cards live under the game panel.
        if (gameState == EGameState.LEVELCOMPLETE)
        {
            Invoke(nameof(ShowLevelCompletePanel), levelCompletePanelDelay);
            return;
        }

        ShowPanel(gameState);
    }

    private void ShowLevelCompletePanel()
        => ShowPanel(EGameState.LEVELCOMPLETE);

    private void ShowPanel(EGameState gameState)
    {
        menuPanel.SetActive(gameState == EGameState.MENU);
        gamePanel.SetActive(gameState == EGameState.GAME);
        levelCompletedPanel.SetActive(gameState == EGameState.LEVELCOMPLETE);
        gameOverPanel.SetActive(gameState == EGameState.GAMEOVER);
    }
}
