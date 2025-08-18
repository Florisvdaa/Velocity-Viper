using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI components")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject overlay;

    [Header("Buttons")]
    [SerializeField] private Button startButton;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }

        startButton.onClick.AddListener(() => StartGame());
    }

    private void Update()
    {
        if (scoreText != null)
            scoreText.text = GameManager.Instance.GetScore().ToString();
    }

    private void StartGame()
    {
        if(overlay != null)
            overlay.SetActive(false);

        GameManager.Instance.StartGame();
    }
}
