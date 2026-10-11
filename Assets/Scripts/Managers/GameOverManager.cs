using UnityEngine;
using UnityEngine.UI;

public class GameOverManager : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public CanvasGroup panel;
    public Text finalScoreText;
    public Text bestScoreText;
    public float delay = 1.5f;
    public float fadeSpeed = 1.5f;

    float timer;
    bool shown;

    void Awake()
    {
        if (panel != null)
        {
            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;
        }
    }

    void Update()
    {
        if (playerHealth.currentHealth > 0) return;

        timer += Time.deltaTime;
        if (timer < delay) return;

        if (!shown)
        {
            shown = true;

            int best = PlayerPrefs.GetInt("SurvivorBestScore", 0);
            if (ScoreManager.score > best)
            {
                best = ScoreManager.score;
                PlayerPrefs.SetInt("SurvivorBestScore", best);
                PlayerPrefs.Save();
            }

            if (finalScoreText != null) finalScoreText.text = "Your Score: " + ScoreManager.score;
            if (bestScoreText != null) bestScoreText.text = "Best: " + best;

            if (panel != null)
            {
                panel.interactable = true;
                panel.blocksRaycasts = true;
            }
        }

        if (panel != null)
            panel.alpha = Mathf.MoveTowards(panel.alpha, 1f, fadeSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return))
            playerHealth.RestartLevel();
    }
}
