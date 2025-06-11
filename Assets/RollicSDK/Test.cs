using UnityEngine;
using UnityEngine.UI;

public class Test : MonoBehaviour
{
    [SerializeField] Button GameButtonClick;

    void Awake()
    {
        RollicCaseSDK.Initialize();
        GameButtonClick.onClick.AddListener(OnGameButtonClick);
    }

    private void OnGameButtonClick()
    {
        Debug.Log("clicked to button");
        RollicCaseSDK.TrackEvent("my button click");
    }
}
