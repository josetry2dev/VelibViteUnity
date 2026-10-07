using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Score : MonoBehaviour
{
    public TextMeshProUGUI scoreText;

    private float _startZ;
    private bool displayScore = true;

    void Start()
    {
        // if (ScoreManager.Instance == null)
        // {
        //     displayScore = false;
        // }
    }
    // Update is called once per frame
    void Update()
    {
        if (displayScore)
        {
            // scoreText.text = (ScoreManager.Instance.elapsedTime).ToString("0.00");    
        }
        
    }
}
