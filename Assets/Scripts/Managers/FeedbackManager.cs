using UnityEngine;
using MoreMountains.Feedbacks;
public class FeedbackManager : MonoBehaviour
{
    public static FeedbackManager Instance { get; private set; }


    [SerializeField] private MMF_Player camShakeFeedback;
    [SerializeField] private MMF_Player raiseWallsFeedback;
    [SerializeField] private MMF_Player collapseWallsFeedback;
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

    public MMF_Player CameraShake() => camShakeFeedback;
    public MMF_Player RaiseWalls() => raiseWallsFeedback;
    public MMF_Player collapseWalls() => collapseWallsFeedback;
}
