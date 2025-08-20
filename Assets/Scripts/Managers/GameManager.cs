using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private int score = 0;

    [SerializeField] private Snake snakePlayer;

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
    }
    public void StartGame(bool walls)
    {
        if (snakePlayer != null)
            snakePlayer.ChangeWrap(walls);

        if(snakePlayer != null)
            snakePlayer.StartMovement();
    }

    public void AddScore()
    {
        score++;
    }
    public int GetScore() => score;
}
