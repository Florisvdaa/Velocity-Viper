using UnityEngine;
using MoreMountains.Feedbacks;
public class FeedbackManager : MonoBehaviour
{
    public static FeedbackManager Instance { get; private set; }


    [SerializeField] private MMF_Player camShakeFeedback;
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
}
