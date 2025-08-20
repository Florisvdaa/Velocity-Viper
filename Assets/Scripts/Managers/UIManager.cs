using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI components")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject mainOverlay;
    [SerializeField] private GameObject settingsOverlay;
    [SerializeField] private Toggle wallsToggle;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button returnButton;

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

        SetMainMenu();

        startButton.onClick.AddListener(() => StartGame());
        settingsButton.onClick.AddListener(() => SettingsScreen());
        returnButton.onClick.AddListener(() => SetMainMenu());
    }

    private void Update()
    {
        if (scoreText != null)
            scoreText.text = GameManager.Instance.GetScore().ToString();
    }

    private void StartGame()
    {
        if(mainOverlay != null)
            mainOverlay.SetActive(false);

        Debug.Log(wallsToggle.isOn);

        GameManager.Instance.StartGame(wallsToggle.isOn);
    }

    private void SetMainMenu()
    {
        if (mainOverlay != null)
            mainOverlay.SetActive(true);
        if (settingsOverlay != null)
            settingsOverlay.SetActive(false);
    }

    private void SettingsScreen()
    {
        if (mainOverlay != null)
            mainOverlay.SetActive(false);
        if (settingsOverlay != null)
            settingsOverlay.SetActive(true);
    }
}
